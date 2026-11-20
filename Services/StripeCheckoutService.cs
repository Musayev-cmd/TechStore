using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using TechStore.Data;
using TechStore.Models;
using TechStore.Options;
using TechStore.Services.Localization;

namespace TechStore.Services;

public sealed class StripeCheckoutService : IStripeCheckoutService
{
    private readonly AppDbContext _db;
    private readonly StripeOptions _stripeOptions;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<StripeCheckoutService> _logger;

    public StripeCheckoutService(
        AppDbContext db,
        IOptions<StripeOptions> stripeOptions,
        IStringLocalizer<SharedResource> localizer,
        ILogger<StripeCheckoutService> logger)
    {
        _db = db;
        _stripeOptions = stripeOptions.Value;
        _localizer = localizer;
        _logger = logger;
    }

    public bool IsConfigured => _stripeOptions.IsConfigured;

    public async Task<StripeSessionResult> CreateCheckoutSessionAsync(
        Order order,
        string successUrl,
        string cancelUrl,
        string culture,
        CancellationToken cancellationToken = default)
    {
        if (!_stripeOptions.IsConfigured)
        {
            throw new InvalidOperationException("Stripe SecretKey is not configured.");
        }

        var currency = order.Currency.Trim().ToLowerInvariant();
        var lineItems = order.Items.Select(item => new SessionLineItemOptions
        {
            Quantity = item.Quantity,
            PriceData = new SessionLineItemPriceDataOptions
            {
                Currency = currency,
                UnitAmount = ToMinorUnits(item.UnitPrice),
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = item.ProductNameSnapshot,
                    Description = $"SKU: {item.ProductSkuSnapshot}"
                }
            }
        }).ToList();

        var metadata = new Dictionary<string, string>
        {
            ["order_id"] = order.Id.ToString(),
            ["order_number"] = order.OrderNumber
        };

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            PaymentMethodTypes = ["card"],
            CustomerEmail = order.CustomerEmail,
            ClientReferenceId = order.OrderNumber,
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            LineItems = lineItems,
            Metadata = metadata,
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                Metadata = metadata
            },
            Locale = MapStripeLocale(culture)
        };

        if (order.ShippingAmount > 0m)
        {
            options.ShippingOptions =
            [
                new SessionShippingOptionOptions
                {
                    ShippingRateData = new SessionShippingOptionShippingRateDataOptions
                    {
                        Type = "fixed_amount",
                        DisplayName = _localizer["Checkout.ShippingLineItem"].Value,
                        FixedAmount = new SessionShippingOptionShippingRateDataFixedAmountOptions
                        {
                            Amount = ToMinorUnits(order.ShippingAmount),
                            Currency = currency
                        }
                    }
                }
            ];
        }

        var client = new StripeClient(_stripeOptions.SecretKey);
        var sessionService = new SessionService(client);
        var session = await sessionService.CreateAsync(options, cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(session.Id) || string.IsNullOrWhiteSpace(session.Url))
        {
            throw new InvalidOperationException("Stripe Checkout session did not return a valid redirect URL.");
        }

        return new StripeSessionResult(session.Id, session.Url);
    }

    public async Task<StripeWebhookResult> HandleWebhookAsync(
        string payload,
        string signatureHeader,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_stripeOptions.WebhookSecret))
        {
            _logger.LogError("Stripe webhook rejected: Stripe:WebhookSecret is not configured.");
            return new StripeWebhookResult(
                StatusCodes.Status503ServiceUnavailable,
                "Stripe webhook secret is not configured.");
        }

        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            _logger.LogWarning("Stripe webhook rejected: missing Stripe-Signature header.");
            return new StripeWebhookResult(
                StatusCodes.Status400BadRequest,
                "Missing Stripe-Signature header.");
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                payload,
                signatureHeader,
                _stripeOptions.WebhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature verification failed.");
            return new StripeWebhookResult(
                StatusCodes.Status400BadRequest,
                "Invalid Stripe signature.");
        }
        catch (Exception ex) when (ex is Newtonsoft.Json.JsonException or System.Text.Json.JsonException)
        {
            _logger.LogWarning(ex, "Stripe webhook payload JSON is malformed.");
            return new StripeWebhookResult(
                StatusCodes.Status400BadRequest,
                "Malformed Stripe event payload.");
        }

        if (string.IsNullOrWhiteSpace(stripeEvent.Id))
        {
            return new StripeWebhookResult(
                StatusCodes.Status400BadRequest,
                "Stripe event payload missing event ID.");
        }

        var alreadyProcessed = await _db.ProcessedStripeEvents
            .AsNoTracking()
            .AnyAsync(e => e.EventId == stripeEvent.Id, cancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogInformation("Duplicate Stripe event {EventId} ({EventType}) ignored.", stripeEvent.Id, stripeEvent.Type);
            return new StripeWebhookResult(
                StatusCodes.Status200OK,
                "Event already processed.",
                IsDuplicate: true,
                EventId: stripeEvent.Id,
                EventType: stripeEvent.Type);
        }

        var nowUtc = DateTime.UtcNow;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _db.ProcessedStripeEvents.Add(new ProcessedStripeEvent
            {
                EventId = stripeEvent.Id,
                EventType = stripeEvent.Type ?? string.Empty,
                ProcessedAtUtc = nowUtc
            });

            await _db.SaveChangesAsync(cancellationToken);

            string? processedOrderNumber = null;

            if (string.Equals(stripeEvent.Type, EventTypes.CheckoutSessionCompleted, StringComparison.Ordinal))
            {
                if (stripeEvent.Data.Object is Session session)
                {
                    processedOrderNumber = await FulfillCheckoutSessionAsync(session, nowUtc, cancellationToken);
                }
            }
            else if (string.Equals(stripeEvent.Type, EventTypes.CheckoutSessionExpired, StringComparison.Ordinal))
            {
                if (stripeEvent.Data.Object is Session session)
                {
                    processedOrderNumber = await ExpireCheckoutSessionAsync(session, nowUtc, cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Processed Stripe event {EventId} ({EventType}) for order {OrderNumber}.",
                stripeEvent.Id,
                stripeEvent.Type,
                processedOrderNumber ?? "n/a");

            return new StripeWebhookResult(
                StatusCodes.Status200OK,
                "Processed.",
                IsDuplicate: false,
                EventId: stripeEvent.Id,
                EventType: stripeEvent.Type,
                OrderNumber: processedOrderNumber);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();

            var existsNow = await _db.ProcessedStripeEvents
                .AsNoTracking()
                .AnyAsync(e => e.EventId == stripeEvent.Id, cancellationToken);

            if (existsNow)
            {
                _logger.LogInformation(
                    "Concurrent duplicate Stripe event {EventId} detected via unique constraint; ignoring.",
                    stripeEvent.Id);
                return new StripeWebhookResult(
                    StatusCodes.Status200OK,
                    "Event already processed.",
                    IsDuplicate: true,
                    EventId: stripeEvent.Id,
                    EventType: stripeEvent.Type);
            }

            _logger.LogError(ex, "Database error while processing Stripe event {EventId}.", stripeEvent.Id);
            throw;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<string?> FulfillCheckoutSessionAsync(
        Session session,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(session.PaymentStatus) &&
            !string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(session.PaymentStatus, "no_payment_required", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Stripe session {SessionId} completed with non-paid status '{PaymentStatus}'; awaiting async payment.",
                session.Id,
                session.PaymentStatus);
            return null;
        }

        var order = await FindOrderForSessionAsync(session, cancellationToken);
        if (order is null)
        {
            _logger.LogWarning(
                "No matching TechStore order found for Stripe checkout session {SessionId} (client_reference_id={ClientRef}).",
                session.Id,
                session.ClientReferenceId);
            return null;
        }

        if (order.Status != OrderStatus.Pending)
        {
            _logger.LogInformation(
                "Order {OrderNumber} is already in status {Status}; skipping fulfillment mutation.",
                order.OrderNumber,
                order.Status);
            return order.OrderNumber;
        }

        // Atomically decrement product stock via parameterized SQL inside the active transaction
        foreach (var item in order.Items)
        {
            if (!item.ProductId.HasValue || item.Quantity <= 0)
            {
                continue;
            }

            var productId = item.ProductId.Value;
            var qty = item.Quantity;

            var updated = await _db.Products
                .Where(p => p.Id == productId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(p => p.StockQuantity, p => p.StockQuantity >= qty ? p.StockQuantity - qty : 0)
                        .SetProperty(p => p.UpdatedAtUtc, nowUtc),
                    cancellationToken);

            if (updated == 0)
            {
                _logger.LogWarning(
                    "Product {ProductId} referenced by order {OrderNumber} was not found during atomic stock decrement.",
                    productId,
                    order.OrderNumber);
            }
        }

        order.Status = OrderStatus.Paid;
        order.StripeCheckoutSessionId = string.IsNullOrWhiteSpace(order.StripeCheckoutSessionId)
            ? session.Id
            : order.StripeCheckoutSessionId;
        order.StripePaymentIntentId = session.PaymentIntentId;
        order.PaidAtUtc = nowUtc;
        order.UpdatedAtUtc = nowUtc;

        await _db.SaveChangesAsync(cancellationToken);

        // Clear the buyer's persistent cart inside the same transaction
        if (!string.IsNullOrWhiteSpace(order.UserId))
        {
            await _db.CartItems
                .Where(c => c.UserId == order.UserId)
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(order.GuestSessionId))
        {
            await _db.CartItems
                .Where(c => c.GuestSessionId == order.GuestSessionId)
                .ExecuteDeleteAsync(cancellationToken);
        }

        return order.OrderNumber;
    }

    private async Task<string?> ExpireCheckoutSessionAsync(
        Session session,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var order = await FindOrderForSessionAsync(session, cancellationToken);
        if (order is null || order.Status != OrderStatus.Pending)
        {
            return order?.OrderNumber;
        }

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAtUtc = nowUtc;
        await _db.SaveChangesAsync(cancellationToken);
        return order.OrderNumber;
    }

    private async Task<Order?> FindOrderForSessionAsync(Session session, CancellationToken cancellationToken)
    {
        Order? order = null;

        if (!string.IsNullOrWhiteSpace(session.Id))
        {
            order = await _db.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.StripeCheckoutSessionId == session.Id, cancellationToken);
        }

        if (order is null &&
            session.Metadata is not null &&
            session.Metadata.TryGetValue("order_id", out var orderIdRaw) &&
            int.TryParse(orderIdRaw, out var orderId))
        {
            order = await _db.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        }

        if (order is null)
        {
            var orderNumber = session.Metadata is not null && session.Metadata.TryGetValue("order_number", out var metaNumber)
                ? metaNumber
                : session.ClientReferenceId;

            if (!string.IsNullOrWhiteSpace(orderNumber))
            {
                order = await _db.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, cancellationToken);
            }
        }

        return order;
    }

    private static long ToMinorUnits(decimal amount) =>
        Convert.ToInt64(Math.Round(amount * 100m, MidpointRounding.AwayFromZero));

    private static string MapStripeLocale(string culture) =>
        JsonLocalizationStore.NormalizeCulture(culture) switch
        {
            "ru" => "ru",
            "en" => "en",
            _ => "auto"
        };
}
