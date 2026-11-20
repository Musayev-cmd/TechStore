namespace TechStore.ViewModels;

public sealed class HomeViewModel
{
    public int TotalCategories { get; init; }
    public int TotalProducts { get; init; }
    public int InStockProducts { get; init; }
    public int LowStockThreshold { get; init; }
    public decimal FreeShippingThreshold { get; init; }
    public string FormattedFreeShippingThreshold { get; init; } = string.Empty;
    public ProductCardViewModel? HeroProduct { get; init; }
    public IReadOnlyList<CategoryFacetViewModel> Categories { get; init; } = Array.Empty<CategoryFacetViewModel>();
    public IReadOnlyList<ProductCardViewModel> FeaturedProducts { get; init; } = Array.Empty<ProductCardViewModel>();
    public IReadOnlyList<ProductCardViewModel> DealProducts { get; init; } = Array.Empty<ProductCardViewModel>();
    public IReadOnlyList<BrandFacetViewModel> Brands { get; init; } = Array.Empty<BrandFacetViewModel>();
}
