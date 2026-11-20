using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using TechStore.Data;
using TechStore.Models;
using TechStore.Options;
using TechStore.Services.Localization;
using TechStore.ViewModels;

namespace TechStore.Services;

public sealed class CartService : ICartService
{
    private readonly AppDbContext _db;
    private readonly ICurrencyFormatter _currency;
    private readonly StoreOptions _storeOptions;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public CartService(
        AppDbContext db,
        ICurrencyFormatter currency,
        IOptions<StoreOptions> storeOptions,
        IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _currency = currency;
        _storeOptions = storeOptions.Value;
        _localizer = localizer;
    }

    public async Task<CartViewModel> GetCartAsync(CartOwner owner, string culture, CancellationToken cancellationToken = default)
    {
        var normalizedCulture = JsonLocalizationStore.NormalizeCulture(culture);

        if (!owner.HasIdentity)
        {
            return BuildEmptyCart(owner, stockNotice: null);
        }

        var rows = await QueryOwnerItems(owner)
            .Include(c => c.Product)
                .ThenInclude(p => p!.Images)
            .Include(c => c.Product)
                .ThenInclude(p => p!.Category)
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken);

        var adjusted = false;
        var validRows = new List<CartItem>();

        foreach (var row in rows)
        {
            if (row.Product is null || !row.Product.IsActive || (row.Product.Category != null && !row.Product.Category.IsActive) || row.Product.StockQuantity <= 0)
            {
                _db.CartItems.Remove(row);
                adjusted = true;
                continue;
            }

            if (row.Quantity <= 0)
            {
                _db.CartItems.Remove(row);
                adjusted = true;
                continue;
            }

            if (row.Quantity > row.Product.StockQuantity)
            {
                row.Quantity = row.Product.StockQuantity;
                row.UpdatedAtUtc = DateTime.UtcNow;
                adjusted = true;
            }

            validRows.Add(row);
        }

        if (adjusted)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return BuildCartViewModel(
            owner,
            validRows,
            normalizedCulture,
            adjusted ? _localizer["Cart.StockAdjusted"].Value : null);
    }

    public async Task<CartMutationResult> AddItemAsync(
        CartOwner owner,
        int productId,
        int quantity,
        string culture,
        CancellationToken cancellationToken = default)
    {
        var normalizedCulture = JsonLocalizationStore.NormalizeCulture(culture);

        if (!owner.HasIdentity || productId <= 0 || quantity <= 0)
        {
            return new CartMutationResult
            {
                Succeeded = false,
                ErrorMessage = _localizer["Cart.ErrorGeneric"].Value,
                Cart = await GetCartAsync(owner, normalizedCulture, cancellationToken)
            };
        }

        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(
                p => p.Id == productId && p.IsActive && (p.Category == null || p.Category.IsActive),
                cancellationToken);

        if (product is null)
        {
            return new CartMutationResult
            {
                Succeeded = false,
                ErrorMessage = _localizer["Product.NotFound"].Value,
                Cart = await GetCartAsync(owner, normalizedCulture, cancellationToken)
            };
        }

        if (product.StockQuantity <= 0)
        {
            return new CartMutationResult
            {
                Succeeded = false,
                ErrorMessage = _localizer["Js.OutOfStock"].Value,
                Cart = await GetCartAsync(owner, normalizedCulture, cancellationToken)
            };
        }

        var existing = await QueryOwnerItems(owner)
            .FirstOrDefaultAsync(c => c.ProductId == productId, cancellationToken);

        var clamped = false;
        var nowUtc = DateTime.UtcNow;

        if (existing is not null)
        {
            var desired = existing.Quantity + quantity;
            if (desired > product.StockQuantity)
            {
                desired = product.StockQuantity;
                clamped = true;
            }

            existing.Quantity = desired;
            existing.UpdatedAtUtc = nowUtc;
        }
        else
        {
            var desired = quantity;
            if (desired > product.StockQuantity)
            {
                desired = product.StockQuantity;
                clamped = true;
            }

            _db.CartItems.Add(new CartItem
            {
                UserId = owner.UserId,
                GuestSessionId = owner.IsAuthenticated ? null : owner.GuestSessionId,
                ProductId = productId,
                Quantity = desired,
                UpdatedAtUtc = nowUtc
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        var cart = await GetCartAsync(owner, normalizedCulture, cancellationToken);
        return new CartMutationResult
        {
            Succeeded = true,
            ClampedToStock = clamped,
            Cart = cart
        };
    }

    public async Task<CartMutationResult> UpdateQuantityAsync(
        CartOwner owner,
        int productId,
        int quantity,
        string culture,
        CancellationToken cancellationToken = default)
    {
        var normalizedCulture = JsonLocalizationStore.NormalizeCulture(culture);

        if (!owner.HasIdentity || productId <= 0)
        {
            return new CartMutationResult
            {
                Succeeded = false,
                ErrorMessage = _localizer["Cart.ErrorGeneric"].Value,
                Cart = await GetCartAsync(owner, normalizedCulture, cancellationToken)
            };
        }

        var existing = await QueryOwnerItems(owner)
            .Include(c => c.Product)
            .FirstOrDefaultAsync(c => c.ProductId == productId, cancellationToken);

        if (existing is null)
        {
            return new CartMutationResult
            {
                Succeeded = true,
                Cart = await GetCartAsync(owner, normalizedCulture, cancellationToken)
            };
        }

        if (quantity <= 0 || existing.Product is null || !existing.Product.IsActive || existing.Product.StockQuantity <= 0)
        {
            _db.CartItems.Remove(existing);
            await _db.SaveChangesAsync(cancellationToken);

            return new CartMutationResult
            {
                Succeeded = true,
                Cart = await GetCartAsync(owner, normalizedCulture, cancellationToken)
            };
        }

        var clamped = false;
        var nextQty = quantity;
        if (nextQty > existing.Product.StockQuantity)
        {
            nextQty = existing.Product.StockQuantity;
            clamped = true;
        }

        existing.Quantity = nextQty;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new CartMutationResult
        {
            Succeeded = true,
            ClampedToStock = clamped,
            Cart = await GetCartAsync(owner, normalizedCulture, cancellationToken)
        };
    }

    public async Task<CartViewModel> RemoveItemAsync(
        CartOwner owner,
        int productId,
        string culture,
        CancellationToken cancellationToken = default)
    {
        var normalizedCulture = JsonLocalizationStore.NormalizeCulture(culture);

        if (owner.HasIdentity && productId > 0)
        {
            var items = await QueryOwnerItems(owner)
                .Where(c => c.ProductId == productId)
                .ToListAsync(cancellationToken);

            if (items.Count > 0)
            {
                _db.CartItems.RemoveRange(items);
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        return await GetCartAsync(owner, normalizedCulture, cancellationToken);
    }

    public async Task<CartViewModel> ClearCartAsync(
        CartOwner owner,
        string culture,
        CancellationToken cancellationToken = default)
    {
        var normalizedCulture = JsonLocalizationStore.NormalizeCulture(culture);

        if (owner.HasIdentity)
        {
            var items = await QueryOwnerItems(owner).ToListAsync(cancellationToken);
            if (items.Count > 0)
            {
                _db.CartItems.RemoveRange(items);
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        return BuildEmptyCart(owner, stockNotice: null);
    }

    public async Task<CartViewModel> SyncGuestItemsAsync(
        CartOwner owner,
        IReadOnlyList<SyncCartItemDto> items,
        string culture,
        CancellationToken cancellationToken = default)
    {
        var normalizedCulture = JsonLocalizationStore.NormalizeCulture(culture);

        if (!owner.HasIdentity || items.Count == 0)
        {
            return await GetCartAsync(owner, normalizedCulture, cancellationToken);
        }

        var requestedByProduct = items
            .Where(i => i.ProductId > 0 && i.Quantity > 0)
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        if (requestedByProduct.Count == 0)
        {
            return await GetCartAsync(owner, normalizedCulture, cancellationToken);
        }

        var productIds = requestedByProduct.Keys.ToList();
        var products = await _db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id) && p.IsActive && p.StockQuantity > 0)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var existingRows = await QueryOwnerItems(owner)
            .ToDictionaryAsync(c => c.ProductId, cancellationToken);

        var nowUtc = DateTime.UtcNow;

        foreach (var (productId, qty) in requestedByProduct)
        {
            if (!products.TryGetValue(productId, out var product))
            {
                continue;
            }

            if (existingRows.TryGetValue(productId, out var existing))
            {
                existing.Quantity = Math.Min(product.StockQuantity, Math.Max(existing.Quantity, qty));
                existing.UpdatedAtUtc = nowUtc;
            }
            else
            {
                _db.CartItems.Add(new CartItem
                {
                    UserId = owner.UserId,
                    GuestSessionId = owner.IsAuthenticated ? null : owner.GuestSessionId,
                    ProductId = productId,
                    Quantity = Math.Min(product.StockQuantity, qty),
                    UpdatedAtUtc = nowUtc
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await GetCartAsync(owner, normalizedCulture, cancellationToken);
    }

    public async Task MergeGuestCartAsync(string guestSessionId, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(guestSessionId) || string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var guestItems = await _db.CartItems
            .Include(c => c.Product)
            .Where(c => c.GuestSessionId == guestSessionId && c.UserId == null)
            .ToListAsync(cancellationToken);

        if (guestItems.Count == 0)
        {
            return;
        }

        var userItems = await _db.CartItems
            .Where(c => c.UserId == userId)
            .ToDictionaryAsync(c => c.ProductId, cancellationToken);

        var nowUtc = DateTime.UtcNow;

        foreach (var guestItem in guestItems)
        {
            var product = guestItem.Product;
            if (product is null || !product.IsActive || product.StockQuantity <= 0 || guestItem.Quantity <= 0)
            {
                _db.CartItems.Remove(guestItem);
                continue;
            }

            if (userItems.TryGetValue(guestItem.ProductId, out var existingUserItem))
            {
                existingUserItem.Quantity = Math.Min(product.StockQuantity, existingUserItem.Quantity + guestItem.Quantity);
                existingUserItem.UpdatedAtUtc = nowUtc;
                _db.CartItems.Remove(guestItem);
            }
            else
            {
                guestItem.UserId = userId;
                guestItem.GuestSessionId = null;
                guestItem.Quantity = Math.Min(product.StockQuantity, guestItem.Quantity);
                guestItem.UpdatedAtUtc = nowUtc;
                userItems[guestItem.ProductId] = guestItem;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<CartItem> QueryOwnerItems(CartOwner owner)
    {
        return owner.IsAuthenticated
            ? _db.CartItems.Where(c => c.UserId == owner.UserId)
            : _db.CartItems.Where(c => c.GuestSessionId == owner.GuestSessionId && c.UserId == null);
    }

    private CartViewModel BuildEmptyCart(CartOwner owner, string? stockNotice)
    {
        return new CartViewModel
        {
            Items = Array.Empty<CartItemViewModel>(),
            ItemCount = 0,
            UniqueItemCount = 0,
            Subtotal = 0m,
            ShippingAmount = 0m,
            Total = 0m,
            FreeShippingThreshold = _storeOptions.FreeShippingThreshold,
            FreeShippingRemaining = _storeOptions.FreeShippingThreshold,
            QualifiesForFreeShipping = false,
            FreeShippingProgressPercent = 0,
            CurrencyCode = _currency.CurrencyCode,
            CurrencySymbol = _currency.CurrencySymbol,
            FormattedSubtotal = _currency.Format(0m),
            FormattedShipping = _localizer["Cart.FreeShipping"].Value,
            FormattedTotal = _currency.Format(0m),
            FormattedFreeShippingRemaining = _currency.Format(_storeOptions.FreeShippingThreshold),
            FormattedFreeShippingThreshold = _currency.Format(_storeOptions.FreeShippingThreshold),
            IsAuthenticated = owner.IsAuthenticated,
            HasServerSession = owner.HasIdentity,
            StockNotice = stockNotice
        };
    }

    private CartViewModel BuildCartViewModel(
        CartOwner owner,
        List<CartItem> rows,
        string culture,
        string? stockNotice)
    {
        if (rows.Count == 0)
        {
            return BuildEmptyCart(owner, stockNotice);
        }

        var items = rows.Select(row =>
        {
            var product = row.Product!;
            var primaryImage = product.Images
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.DisplayOrder)
                .ThenBy(i => i.Id)
                .FirstOrDefault();

            var unitPrice = product.EffectivePrice;
            var lineTotal = unitPrice * row.Quantity;

            return new CartItemViewModel
            {
                ProductId = product.Id,
                ProductSlug = product.Slug,
                ProductUrl = $"/Catalog/Details/{product.Slug}",
                Sku = product.Sku,
                Brand = product.Brand,
                Name = product.GetName(culture),
                ImageUrl = primaryImage?.Url ?? string.Empty,
                UnitPrice = unitPrice,
                OriginalPrice = product.Price,
                HasDiscount = product.HasDiscount,
                Quantity = row.Quantity,
                MaxStock = product.StockQuantity,
                LineTotal = lineTotal,
                FormattedUnitPrice = _currency.Format(unitPrice),
                FormattedOriginalPrice = _currency.Format(product.Price),
                FormattedLineTotal = _currency.Format(lineTotal),
                StockState = product.StockQuantity <= _storeOptions.LowStockThreshold ? "low_stock" : "in_stock"
            };
        }).ToList();

        var subtotal = items.Sum(i => i.LineTotal);
        var threshold = _storeOptions.FreeShippingThreshold;
        var qualifiesForFree = threshold <= 0m || subtotal >= threshold;
        var shippingAmount = qualifiesForFree ? 0m : _storeOptions.FlatShippingFee;
        var total = subtotal + shippingAmount;
        var remaining = qualifiesForFree ? 0m : Math.Max(0m, threshold - subtotal);
        var progressPercent = threshold <= 0m
            ? 100
            : (int)Math.Clamp(Math.Round((subtotal / threshold) * 100m, MidpointRounding.AwayFromZero), 0m, 100m);

        return new CartViewModel
        {
            Items = items,
            ItemCount = items.Sum(i => i.Quantity),
            UniqueItemCount = items.Count,
            Subtotal = subtotal,
            ShippingAmount = shippingAmount,
            Total = total,
            FreeShippingThreshold = threshold,
            FreeShippingRemaining = remaining,
            QualifiesForFreeShipping = qualifiesForFree,
            FreeShippingProgressPercent = progressPercent,
            CurrencyCode = _currency.CurrencyCode,
            CurrencySymbol = _currency.CurrencySymbol,
            FormattedSubtotal = _currency.Format(subtotal),
            FormattedShipping = shippingAmount == 0m
                ? _localizer["Cart.FreeShipping"].Value
                : _currency.Format(shippingAmount),
            FormattedTotal = _currency.Format(total),
            FormattedFreeShippingRemaining = _currency.Format(remaining),
            FormattedFreeShippingThreshold = _currency.Format(threshold),
            IsAuthenticated = owner.IsAuthenticated,
            HasServerSession = owner.HasIdentity,
            StockNotice = stockNotice
        };
    }
}
