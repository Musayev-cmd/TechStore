using TechStore.Models;

namespace TechStore.ViewModels;

public sealed class HomeCategoryCardViewModel
{
    public int Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string IconName { get; init; } = "cpu";
    public int ProductCount { get; init; }
}

public sealed class HomeProductCardViewModel
{
    public int Id { get; init; }
    public string Sku { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ShortDescription { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal? DiscountPrice { get; init; }
    public decimal EffectivePrice { get; init; }
    public bool HasDiscount { get; init; }
    public int DiscountPercent { get; init; }
    public int StockQuantity { get; init; }
    public string ImageUrl { get; init; } = string.Empty;
}

public sealed class HomeViewModel
{
    public int TotalCategories { get; init; }
    public int TotalProducts { get; init; }
    public int LowStockThreshold { get; init; }
    public HomeProductCardViewModel? HeroProduct { get; init; }
    public IReadOnlyList<HomeCategoryCardViewModel> Categories { get; init; } = Array.Empty<HomeCategoryCardViewModel>();
    public IReadOnlyList<HomeProductCardViewModel> FeaturedProducts { get; init; } = Array.Empty<HomeProductCardViewModel>();
}
