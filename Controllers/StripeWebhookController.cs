using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Services;

namespace TechStore.Controllers;

[ApiController]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route("webhooks/stripe")]
public sealed class StripeWebhookController : ControllerBase
{
    private readonly IStripeCheckoutService _stripeCheckoutService;

    public StripeWebhookController(IStripeCheckoutService stripeCheckoutService)
    {
        _stripeCheckoutService = stripeCheckoutService;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        string payload;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            payload = await reader.ReadToEndAsync(cancellationToken);
        }

        var signatureHeader = Request.Headers["Stripe-Signature"].ToString();
        var result = await _stripeCheckoutService.HandleWebhookAsync(payload, signatureHeader, cancellationToken);

        return StatusCode(result.StatusCode, new
        {
            received = result.StatusCode == StatusCodes.Status200OK,
            duplicate = result.IsDuplicate,
            eventId = result.EventId,
            eventType = result.EventType,
            orderNumber = result.OrderNumber,
            message = result.Message
        });
    }
}
