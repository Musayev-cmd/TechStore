using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using TechStore.Services;
using TechStore.Services.Localization;
using TechStore.ViewModels;

namespace TechStore.Controllers;

[ApiController]
[Route("api/cart")]
public sealed class CartApiController : ControllerBase
{
    private readonly ICartService _cartService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public CartApiController(
        ICartService cartService,
        IStringLocalizer<SharedResource> localizer)
    {
        _cartService = cartService;
        _localizer = localizer;
    }

    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [ProducesResponseType(typeof(CartViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCart(CancellationToken cancellationToken)
    {
        var owner = CartSessionCookieHelper.ResolveOwner(HttpContext, createGuestCookieIfMissing: false);
        var culture = LocalizationHelper.CurrentCultureCode();
        var cart = await _cartService.GetCartAsync(owner, culture, cancellationToken);
        return Ok(cart);
    }

    [HttpPost("items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddItem([FromBody] AddToCartRequest request, CancellationToken cancellationToken)
    {
        var owner = CartSessionCookieHelper.ResolveOwner(HttpContext, createGuestCookieIfMissing: true);
        var culture = LocalizationHelper.CurrentCultureCode();
        var result = await _cartService.AddItemAsync(owner, request.ProductId, request.Quantity, culture, cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                error = result.ErrorMessage ?? _localizer["Cart.ErrorGeneric"].Value,
                cart = result.Cart
            });
        }

        return Ok(new
        {
            cart = result.Cart,
            clampedToStock = result.ClampedToStock,
            message = result.ClampedToStock
                ? _localizer["Js.MaxStockReached"].Value
                : _localizer["Js.AddedToCart"].Value
        });
    }

    [HttpPut("items/{productId:int}")]
    [HttpPatch("items/{productId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateItem(
        int productId,
        [FromBody] UpdateCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var owner = CartSessionCookieHelper.ResolveOwner(HttpContext, createGuestCookieIfMissing: true);
        var culture = LocalizationHelper.CurrentCultureCode();
        var result = await _cartService.UpdateQuantityAsync(owner, productId, request.Quantity, culture, cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                error = result.ErrorMessage ?? _localizer["Cart.ErrorGeneric"].Value,
                cart = result.Cart
            });
        }

        return Ok(new
        {
            cart = result.Cart,
            clampedToStock = result.ClampedToStock,
            message = result.ClampedToStock
                ? _localizer["Js.MaxStockReached"].Value
                : (request.Quantity <= 0
                    ? _localizer["Cart.ItemRemoved"].Value
                    : _localizer["Cart.Updated"].Value)
        });
    }

    [HttpDelete("items/{productId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveItem(int productId, CancellationToken cancellationToken)
    {
        var owner = CartSessionCookieHelper.ResolveOwner(HttpContext, createGuestCookieIfMissing: false);
        var culture = LocalizationHelper.CurrentCultureCode();
        var cart = await _cartService.RemoveItemAsync(owner, productId, culture, cancellationToken);

        return Ok(new
        {
            cart,
            message = _localizer["Cart.ItemRemoved"].Value
        });
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ClearCart(CancellationToken cancellationToken)
    {
        var owner = CartSessionCookieHelper.ResolveOwner(HttpContext, createGuestCookieIfMissing: false);
        var culture = LocalizationHelper.CurrentCultureCode();
        var cart = await _cartService.ClearCartAsync(owner, culture, cancellationToken);

        return Ok(new
        {
            cart,
            message = _localizer["Cart.Cleared"].Value
        });
    }

    [HttpPost("sync")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SyncGuestItems([FromBody] SyncCartRequest request, CancellationToken cancellationToken)
    {
        var owner = CartSessionCookieHelper.ResolveOwner(HttpContext, createGuestCookieIfMissing: true);
        var culture = LocalizationHelper.CurrentCultureCode();
        var cart = await _cartService.SyncGuestItemsAsync(owner, request.Items ?? [], culture, cancellationToken);

        return Ok(new
        {
            cart,
            message = _localizer["Cart.Updated"].Value
        });
    }
}
