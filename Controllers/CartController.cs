using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using TechStore.Services;
using TechStore.Services.Localization;

namespace TechStore.Controllers;

public sealed class CartController : Controller
{
    private readonly ICartService _cartService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public CartController(
        ICartService cartService,
        IStringLocalizer<SharedResource> localizer)
    {
        _cartService = cartService;
        _localizer = localizer;
    }

    [HttpGet("/Cart")]
    [HttpGet("/Cart/Index")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var owner = CartSessionCookieHelper.ResolveOwner(HttpContext, createGuestCookieIfMissing: false);
        var culture = LocalizationHelper.CurrentCultureCode();
        var cart = await _cartService.GetCartAsync(owner, culture, cancellationToken);

        ViewData["Title"] = _localizer["Cart.Title"].Value;
        return View(cart);
    }
}
