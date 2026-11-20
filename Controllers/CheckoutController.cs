using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Stripe;
using TechStore.Data;
using TechStore.Models;
using TechStore.Options;
using TechStore.Services;
using TechStore.Services.Localization;
using TechStore.ViewModels;

namespace TechStore.Controllers;

public sealed class CheckoutController : Controller
{
    private const string OrderCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly AppDbContext _db;
    private readonly ICartService _cartService;
    private readonly IStripeCheckoutService _stripeCheckoutService;
    private readonly ICurrencyFormatter _currency;
    private readonly TechStore.Options.StoreOptions _storeOptions;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(
        AppDbContext db,
        ICartService cartService,
        IStripeCheckoutService stripeCheckoutService,
        ICurrencyFormatter currency,
        IOptions<TechStore.Options.StoreOptions> storeOptions,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<SharedResource> localizer,
        ILogger<CheckoutController> logger)
    {
        _db = db;
        _cartService = cartService;
        _stripeCheckoutService = stripeCheckoutService;
        _currency = currency;
        _storeOptions = storeOptions.Value;
        _userManager = userManager;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpGet("/Checkout")]
    [HttpGet("/Checkout/Index")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var owner = CartSessionCookieHelper.ResolveOwner(HttpContext, createGuestCookieIfMissing: false);
        var culture = LocalizationHelper.CurrentCultureCode();
        var cart = await _cartService.GetCartAsync(owner, culture, cancellationToken);

        if (cart.Items.Count == 0)
        {
            TempData["CartNotice"] = _localizer["Cart.EmptyRedirectNotice"].Value;
            return RedirectToAction("Index", "Cart");
        }

        var input = new CheckoutInputModel
        {
            ShippingCountry = "Azerbaijan"
        };

        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is not null)
            {
                input.CustomerName = user.FullName;
                input.CustomerEmail = user.Email ?? string.Empty;
                input.CustomerPhone = user.PhoneNumber ?? string.Empty;
                input.ShippingAddress = user.DefaultShippingAddress ?? string.Empty;
                input.ShippingCity = user.DefaultCity ?? string.Empty;
                input.ShippingPostalCode = user.DefaultPostalCode ?? string.Empty;
                input.ShippingCountry = string.IsNullOrWhiteSpace(user.DefaultCountry)
                    ? "Azerbaijan"
                    : user.DefaultCountry;
            }
        }

        ViewData["Title"] = _localizer["Checkout.Title"].Value;
        return View(new CheckoutViewModel
        {
            Input = input,
            Cart = cart,
            IsAuthenticated = User.Identity?.IsAuthenticated == true,
            StripeConfigured = _stripeCheckoutService.IsConfigured
        });
    }

    [HttpPost("/Checkout")]
    [HttpPost("/Checkout/Index")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(
        [Bind(Prefix = "Input")] CheckoutInputModel input,
        CancellationToken cancellationToken)
    {
        var owner = CartSessionCookieHelper.ResolveOwner(HttpContext, createGuestCookieIfMissing: false);
        var culture = LocalizationHelper.CurrentCultureCode();
        var cart = await _cartService.GetCartAsync(owner, culture, cancellationToken);

        if (cart.Items.Count == 0)
        {
            TempData["CartNotice"] = _localizer["Cart.EmptyRedirectNotice"].Value;
            return RedirectToAction("Index", "Cart");
        }

        ViewData["Title"] = _localizer["Checkout.Title"].Value;

        if (!ModelState.IsValid)
        {
            return View(BuildCheckoutViewModel(input, cart));
        }

        if (!_stripeCheckoutService.IsConfigured)
        {
            ModelState.AddModelError(string.Empty, _localizer["Checkout.StripeNotConfigured"].Value);
            return View(BuildCheckoutViewModel(input, cart));
        }

        // Server-authoritative inventory and pricing verification directly from database
        var cartRows = await (owner.IsAuthenticated
                ? _db.CartItems.Where(c => c.UserId == owner.UserId)
                : _db.CartItems.Where(c => c.GuestSessionId == owner.GuestSessionId && c.UserId == null))
            .Include(c => c.Product)
                .ThenInclude(p => p!.Images)
            .Include(c => c.Product)
                .ThenInclude(p => p!.Category)
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken);

        if (cartRows.Count == 0)
        {
            TempData["CartNotice"] = _localizer["Cart.EmptyRedirectNotice"].Value;
            return RedirectToAction("Index", "Cart");
        }

        var orderItems = new List<OrderItem>();
        foreach (var row in cartRows)
        {
            var product = row.Product;
            if (product is null || !product.IsActive || (product.Category != null && !product.Category.IsActive))
            {
                var label = product?.GetName(culture) ?? $"#{row.ProductId}";
                ModelState.AddModelError(string.Empty, _localizer["Checkout.ProductUnavailable", label].Value);
                continue;
            }

            if (product.StockQuantity < row.Quantity)
            {
                ModelState.AddModelError(
                    string.Empty,
                    _localizer["Checkout.StockInsufficient", product.GetName(culture), product.StockQuantity].Value);
                continue;
            }

            var primaryImage = product.Images
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.DisplayOrder)
                .ThenBy(i => i.Id)
                .FirstOrDefault();

            var unitPrice = product.EffectivePrice;
            orderItems.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductNameSnapshot = product.GetName(culture),
                ProductSkuSnapshot = product.Sku,
                ProductImageSnapshot = primaryImage?.Url,
                UnitPrice = unitPrice,
                Quantity = row.Quantity,
                LineTotal = unitPrice * row.Quantity
            });
        }

        if (!ModelState.IsValid)
        {
            cart = await _cartService.GetCartAsync(owner, culture, cancellationToken);
            return View(BuildCheckoutViewModel(input, cart));
        }

        var subtotal = orderItems.Sum(i => i.LineTotal);
        var shippingAmount = (_storeOptions.FreeShippingThreshold <= 0m || subtotal >= _storeOptions.FreeShippingThreshold)
            ? 0m
            : _storeOptions.FlatShippingFee;
        var totalAmount = subtotal + shippingAmount;
        var nowUtc = DateTime.UtcNow;

        var order = new Order
        {
            OrderNumber = await GenerateUniqueOrderNumberAsync(cancellationToken),
            UserId = owner.UserId,
            GuestSessionId = owner.IsAuthenticated ? null : owner.GuestSessionId,
            CustomerName = input.CustomerName.Trim(),
            CustomerEmail = input.CustomerEmail.Trim(),
            CustomerPhone = input.CustomerPhone.Trim(),
            ShippingAddress = input.ShippingAddress.Trim(),
            ShippingCity = input.ShippingCity.Trim(),
            ShippingPostalCode = input.ShippingPostalCode.Trim(),
            ShippingCountry = input.ShippingCountry.Trim(),
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
            Currency = _storeOptions.CurrencyCode,
            SubtotalAmount = subtotal,
            ShippingAmount = shippingAmount,
            TotalAmount = totalAmount,
            Status = OrderStatus.Pending,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Items = orderItems
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        if (owner.IsAuthenticated && input.SaveAddressToProfile)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is not null)
            {
                user.FullName = order.CustomerName;
                user.PhoneNumber = order.CustomerPhone;
                user.DefaultShippingAddress = order.ShippingAddress;
                user.DefaultCity = order.ShippingCity;
                user.DefaultPostalCode = order.ShippingPostalCode;
                user.DefaultCountry = order.ShippingCountry;
                await _userManager.UpdateAsync(user);
            }
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        var successUrl = $"{baseUrl}/Checkout/Success?session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = $"{baseUrl}/Checkout/Cancel?order={Uri.EscapeDataString(order.OrderNumber)}";

        try
        {
            var session = await _stripeCheckoutService.CreateCheckoutSessionAsync(
                order,
                successUrl,
                cancelUrl,
                culture,
                cancellationToken);

            order.StripeCheckoutSessionId = session.SessionId;
            order.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            return Redirect(session.CheckoutUrl);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error creating Checkout Session for order {OrderNumber}.", order.OrderNumber);
            order.Status = OrderStatus.Cancelled;
            order.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            ModelState.AddModelError(string.Empty, _localizer["Checkout.StripeError"].Value);
            return View(BuildCheckoutViewModel(input, cart));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating Checkout Session for order {OrderNumber}.", order.OrderNumber);
            order.Status = OrderStatus.Cancelled;
            order.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            ModelState.AddModelError(string.Empty, _localizer["Checkout.StripeError"].Value);
            return View(BuildCheckoutViewModel(input, cart));
        }
    }

    [HttpGet("/Checkout/Success")]
    public async Task<IActionResult> Success(
        [FromQuery(Name = "session_id")] string? sessionId,
        [FromQuery(Name = "order")] string? orderNumber,
        CancellationToken cancellationToken)
    {
        var order = await FindAuthorizedOrderAsync(sessionId, orderNumber, cancellationToken);
        if (order is null)
        {
            ViewData["Title"] = _localizer["Checkout.SuccessTitle"].Value;
            return View("Success", model: null);
        }

        var viewModel = new CheckoutSuccessViewModel
        {
            OrderNumber = order.OrderNumber,
            SessionId = sessionId,
            Status = order.Status,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            ShippingAddress = order.ShippingAddress,
            ShippingCity = order.ShippingCity,
            ShippingPostalCode = order.ShippingPostalCode,
            ShippingCountry = order.ShippingCountry,
            FormattedSubtotal = _currency.Format(order.SubtotalAmount),
            FormattedShipping = order.ShippingAmount == 0m
                ? _localizer["Cart.FreeShipping"].Value
                : _currency.Format(order.ShippingAmount),
            FormattedTotal = _currency.Format(order.TotalAmount),
            IsFreeShipping = order.ShippingAmount == 0m,
            CreatedAtUtc = order.CreatedAtUtc,
            PaidAtUtc = order.PaidAtUtc,
            IsAuthenticated = User.Identity?.IsAuthenticated == true,
            Items = order.Items.Select(i => new OrderItemDetailViewModel
            {
                ProductId = i.ProductId,
                ProductSlug = i.Product?.Slug,
                ProductName = i.ProductNameSnapshot,
                ProductSku = i.ProductSkuSnapshot,
                ImageUrl = i.ProductImageSnapshot ?? string.Empty,
                Quantity = i.Quantity,
                FormattedUnitPrice = _currency.Format(i.UnitPrice),
                FormattedLineTotal = _currency.Format(i.LineTotal)
            }).ToList()
        };

        ViewData["Title"] = order.Status == OrderStatus.Pending
            ? _localizer["Checkout.SuccessPendingTitle"].Value
            : _localizer["Checkout.SuccessTitle"].Value;

        return View(viewModel);
    }

    [HttpGet("/Checkout/Status")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Status(
        [FromQuery(Name = "orderNumber")] string? orderNumber,
        [FromQuery(Name = "sessionId")] string? sessionId,
        CancellationToken cancellationToken)
    {
        var order = await FindAuthorizedOrderAsync(sessionId, orderNumber, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        return Ok(new OrderStatusPollViewModel
        {
            OrderNumber = order.OrderNumber,
            Status = order.Status.ToString(),
            StatusStep = GetStatusStep(order.Status),
            StatusLabel = _localizer[order.Status.GetStatusResourceKey()].Value,
            IsPaid = order.Status is OrderStatus.Paid or OrderStatus.Processing or OrderStatus.Shipped or OrderStatus.Delivered,
            IsCancelled = order.Status == OrderStatus.Cancelled,
            IsTerminal = order.Status is OrderStatus.Delivered or OrderStatus.Cancelled,
            UpdatedAtUtc = order.UpdatedAtUtc,
            PaidAtUtc = order.PaidAtUtc
        });
    }

    [HttpGet("/Checkout/Cancel")]
    public async Task<IActionResult> Cancel(
        [FromQuery(Name = "order")] string? orderNumber,
        CancellationToken cancellationToken)
    {
        var owner = CartSessionCookieHelper.ResolveOwner(HttpContext, createGuestCookieIfMissing: false);
        var culture = LocalizationHelper.CurrentCultureCode();
        var cart = await _cartService.GetCartAsync(owner, culture, cancellationToken);

        ViewData["Title"] = _localizer["Checkout.CancelTitle"].Value;
        return View(new CheckoutCancelViewModel
        {
            OrderNumber = string.IsNullOrWhiteSpace(orderNumber) ? null : orderNumber.Trim(),
            CartItemCount = cart.ItemCount
        });
    }

    private CheckoutViewModel BuildCheckoutViewModel(CheckoutInputModel input, CartViewModel cart)
    {
        return new CheckoutViewModel
        {
            Input = input,
            Cart = cart,
            IsAuthenticated = User.Identity?.IsAuthenticated == true,
            StripeConfigured = _stripeCheckoutService.IsConfigured
        };
    }

    private async Task<Order?> FindAuthorizedOrderAsync(
        string? sessionId,
        string? orderNumber,
        CancellationToken cancellationToken)
    {
        Order? order = null;

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            order = await _db.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.StripeCheckoutSessionId == sessionId.Trim(), cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(orderNumber))
        {
            order = await _db.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber.Trim(), cancellationToken);
        }

        if (order is null)
        {
            return null;
        }

        if (User.IsInRole(DbSeeder.AdminRole))
        {
            return order;
        }

        var currentUserId = User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;
        var currentGuestId = CartSessionCookieHelper.GetExistingGuestSessionId(Request);

        if (!string.IsNullOrWhiteSpace(order.UserId))
        {
            return string.Equals(order.UserId, currentUserId, StringComparison.Ordinal)
                ? order
                : null;
        }

        var matchesGuestCookie = !string.IsNullOrWhiteSpace(currentGuestId) &&
            string.Equals(order.GuestSessionId, currentGuestId, StringComparison.Ordinal);

        var matchesSessionToken = !string.IsNullOrWhiteSpace(sessionId) &&
            string.Equals(order.StripeCheckoutSessionId, sessionId.Trim(), StringComparison.Ordinal);

        return matchesGuestCookie || matchesSessionToken ? order : null;
    }

    private async Task<string> GenerateUniqueOrderNumberAsync(CancellationToken cancellationToken)
    {
        var dateStamp = DateTime.UtcNow.ToString("yyyyMMdd");
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var suffix = RandomNumberGenerator.GetString(OrderCodeAlphabet, 6);
            var candidate = $"TS-{dateStamp}-{suffix}";
            var exists = await _db.Orders.AsNoTracking().AnyAsync(o => o.OrderNumber == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        return $"TS-{dateStamp}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    }

    internal static int GetStatusStep(OrderStatus status) => status switch
    {
        OrderStatus.Pending => 0,
        OrderStatus.Paid => 1,
        OrderStatus.Processing => 2,
        OrderStatus.Shipped => 3,
        OrderStatus.Delivered => 4,
        _ => -1
    };
}
