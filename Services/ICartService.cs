using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using TechStore.ViewModels;

namespace TechStore.Services;

public sealed record CartOwner(string? UserId, string? GuestSessionId)
{
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(UserId);
    public bool HasIdentity => IsAuthenticated || !string.IsNullOrWhiteSpace(GuestSessionId);
}

public static partial class CartSessionCookieHelper
{
    public const string CookieName = "ts_cart_sid";

    [GeneratedRegex("^[a-f0-9]{16,64}$", RegexOptions.Compiled)]
    private static partial Regex ValidSessionIdRegex();

    public static string? GetExistingGuestSessionId(HttpRequest request)
    {
        if (!request.Cookies.TryGetValue(CookieName, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        return ValidSessionIdRegex().IsMatch(trimmed) ? trimmed : null;
    }

    public static string GetOrCreateGuestSessionId(HttpContext httpContext)
    {
        var existing = GetExistingGuestSessionId(httpContext.Request);
        if (!string.IsNullOrEmpty(existing))
        {
            return existing;
        }

        var generated = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        httpContext.Response.Cookies.Append(CookieName, generated, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = httpContext.Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            Path = "/"
        });

        return generated;
    }

    public static void ClearGuestSessionCookie(HttpContext httpContext)
    {
        if (httpContext.Request.Cookies.ContainsKey(CookieName))
        {
            httpContext.Response.Cookies.Delete(CookieName, new CookieOptions
            {
                Path = "/",
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = httpContext.Request.IsHttps
            });
        }
    }

    public static CartOwner ResolveOwner(HttpContext httpContext, bool createGuestCookieIfMissing)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                return new CartOwner(userId, null);
            }
        }

        var guestId = createGuestCookieIfMissing
            ? GetOrCreateGuestSessionId(httpContext)
            : GetExistingGuestSessionId(httpContext.Request);

        return new CartOwner(null, guestId);
    }
}

public interface ICartService
{
    Task<CartViewModel> GetCartAsync(CartOwner owner, string culture, CancellationToken cancellationToken = default);
    Task<CartMutationResult> AddItemAsync(CartOwner owner, int productId, int quantity, string culture, CancellationToken cancellationToken = default);
    Task<CartMutationResult> UpdateQuantityAsync(CartOwner owner, int productId, int quantity, string culture, CancellationToken cancellationToken = default);
    Task<CartViewModel> RemoveItemAsync(CartOwner owner, int productId, string culture, CancellationToken cancellationToken = default);
    Task<CartViewModel> ClearCartAsync(CartOwner owner, string culture, CancellationToken cancellationToken = default);
    Task<CartViewModel> SyncGuestItemsAsync(CartOwner owner, IReadOnlyList<SyncCartItemDto> items, string culture, CancellationToken cancellationToken = default);
    Task MergeGuestCartAsync(string guestSessionId, string userId, CancellationToken cancellationToken = default);
}
