namespace TechStore.ViewModels;

public sealed class ProductImageViewModel
{
    public int Id { get; init; }
    public string Url { get; init; } = string.Empty;
    public string AltText { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
    public int DisplayOrder { get; init; }
}

public sealed class ProductSpecificationViewModel
{
    public int Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
}

public sealed class ProductDetailViewModel
{
    public int Id { get; init; }
    public string Sku { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    public string CategorySlug { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ShortDescription { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal? DiscountPrice { get; init; }
    public decimal EffectivePrice { get; init; }
    public string FormattedPrice { get; init; } = string.Empty;
    public string FormattedEffectivePrice { get; init; } = string.Empty;
    public decimal SavingsAmount { get; init; }
    public string FormattedSavingsAmount { get; init; } = string.Empty;
    public bool HasDiscount { get; init; }
    public int DiscountPercent { get; init; }
    public int StockQuantity { get; init; }
    public int LowStockThreshold { get; init; }
    public string StockState { get; init; } = "in_stock";
    public bool IsFeatured { get; init; }
    public bool QualifiesForFreeShipping { get; init; }
    public decimal FreeShippingThreshold { get; init; }
    public string FormattedFreeShippingThreshold { get; init; } = string.Empty;
    public decimal FlatShippingFee { get; init; }
    public string FormattedFlatShippingFee { get; init; } = string.Empty;
    public IReadOnlyList<ProductImageViewModel> Images { get; init; } = Array.Empty<ProductImageViewModel>();
    public IReadOnlyList<ProductSpecificationViewModel> Specifications { get; init; } = Array.Empty<ProductSpecificationViewModel>();
    public IReadOnlyList<ProductCardViewModel> RelatedProducts { get; init; } = Array.Empty<ProductCardViewModel>();
}
