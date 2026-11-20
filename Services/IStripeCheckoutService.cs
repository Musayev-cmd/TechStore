using TechStore.Models;

namespace TechStore.Services;

public sealed record StripeSessionResult(string SessionId, string CheckoutUrl);

public sealed record StripeWebhookResult(
    int StatusCode,
    string Message,
    bool IsDuplicate = false,
    string? EventId = null,
    string? EventType = null,
    string? OrderNumber = null);

public interface IStripeCheckoutService
{
    bool IsConfigured { get; }

    Task<StripeSessionResult> CreateCheckoutSessionAsync(
        Order order,
        string successUrl,
        string cancelUrl,
        string culture,
        CancellationToken cancellationToken = default);

    Task<StripeWebhookResult> HandleWebhookAsync(
        string payload,
        string signatureHeader,
        CancellationToken cancellationToken = default);
}
