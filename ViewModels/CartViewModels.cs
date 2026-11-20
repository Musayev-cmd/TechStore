using System.ComponentModel.DataAnnotations;

namespace TechStore.ViewModels;

public sealed class CartItemViewModel
{
    public int ProductId { get; set; }
    public string ProductSlug { get; set; } = string.Empty;
    public string ProductUrl { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal OriginalPrice { get; set; }
    public bool HasDiscount { get; set; }
    public int Quantity { get; set; }
    public int MaxStock { get; set; }
    public decimal LineTotal { get; set; }
    public string FormattedUnitPrice { get; set; } = string.Empty;
    public string FormattedOriginalPrice { get; set; } = string.Empty;
    public string FormattedLineTotal { get; set; } = string.Empty;
    public string StockState { get; set; } = "in_stock";
}

public sealed class CartViewModel
{
    public IReadOnlyList<CartItemViewModel> Items { get; set; } = Array.Empty<CartItemViewModel>();
    public int ItemCount { get; set; }
    public int UniqueItemCount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal Total { get; set; }
    public decimal FreeShippingThreshold { get; set; }
    public decimal FreeShippingRemaining { get; set; }
    public bool QualifiesForFreeShipping { get; set; }
    public int FreeShippingProgressPercent { get; set; }
    public string CurrencyCode { get; set; } = "AZN";
    public string CurrencySymbol { get; set; } = "₼";
    public string FormattedSubtotal { get; set; } = string.Empty;
    public string FormattedShipping { get; set; } = string.Empty;
    public string FormattedTotal { get; set; } = string.Empty;
    public string FormattedFreeShippingRemaining { get; set; } = string.Empty;
    public string FormattedFreeShippingThreshold { get; set; } = string.Empty;
    public bool IsAuthenticated { get; set; }
    public bool HasServerSession { get; set; }
    public string? StockNotice { get; set; }
}

public sealed class CartMutationResult
{
    public bool Succeeded { get; set; }
    public string? ErrorMessage { get; set; }
    public bool ClampedToStock { get; set; }
    public CartViewModel Cart { get; set; } = new();
}

public sealed class AddToCartRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, 999)]
    public int Quantity { get; set; } = 1;
}

public sealed class UpdateCartItemRequest
{
    [Range(0, 999)]
    public int Quantity { get; set; }
}

public sealed class SyncCartItemDto
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, 999)]
    public int Quantity { get; set; }
}

public sealed class SyncCartRequest
{
    public List<SyncCartItemDto> Items { get; set; } = new();
}
