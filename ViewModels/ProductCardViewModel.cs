namespace TechStore.ViewModels;

public sealed class ProductCardViewModel
{
    public int Id { get; init; }
    public string Sku { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public string CategorySlug { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ShortDescription { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal? DiscountPrice { get; init; }
    public decimal EffectivePrice { get; init; }
    public string FormattedPrice { get; init; } = string.Empty;
    public string FormattedEffectivePrice { get; init; } = string.Empty;
    public bool HasDiscount { get; init; }
    public int DiscountPercent { get; init; }
    public int StockQuantity { get; init; }
    public string StockState { get; init; } = "in_stock";
    public bool IsFeatured { get; init; }
    public string ImageUrl { get; init; } = string.Empty;
    public string ImageAlt { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
}
