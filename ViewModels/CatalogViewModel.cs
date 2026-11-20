namespace TechStore.ViewModels;

public sealed class CatalogQuery
{
    public const int DefaultPageSize = 12;
    public const int MaxPageSize = 48;

    public string? Search { get; init; }
    public string? Category { get; init; }
    public IReadOnlyList<string> Brands { get; init; } = Array.Empty<string>();
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public bool InStockOnly { get; init; }
    public bool DealsOnly { get; init; }
    public bool FeaturedOnly { get; init; }
    public string Sort { get; init; } = "featured";
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;

    public static string NormalizeSort(string? sort) =>
        (sort ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "price_asc" => "price_asc",
            "price_desc" => "price_desc",
            "newest" => "newest",
            "name_asc" => "name_asc",
            _ => "featured"
        };

    public static IReadOnlyList<string> ParseBrands(string? brandParam, string[]? repeatedBrands = null)
    {
        var results = new List<string>();

        if (!string.IsNullOrWhiteSpace(brandParam))
        {
            foreach (var part in brandParam.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    results.Add(part);
                }
            }
        }

        if (repeatedBrands is { Length: > 0 })
        {
            foreach (var entry in repeatedBrands)
            {
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                foreach (var part in entry.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!string.IsNullOrWhiteSpace(part))
                    {
                        results.Add(part);
                    }
                }
            }
        }

        return results
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public sealed class CategoryFacetViewModel
{
    public int Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string IconName { get; init; } = "cpu";
    public int ProductCount { get; init; }
}

public sealed class BrandFacetViewModel
{
    public string Name { get; init; } = string.Empty;
    public int ProductCount { get; init; }
}

public sealed class PriceBoundsViewModel
{
    public decimal Min { get; init; }
    public decimal Max { get; init; }
}

public sealed class CatalogFacetsViewModel
{
    public IReadOnlyList<CategoryFacetViewModel> Categories { get; init; } = Array.Empty<CategoryFacetViewModel>();
    public IReadOnlyList<BrandFacetViewModel> Brands { get; init; } = Array.Empty<BrandFacetViewModel>();
    public PriceBoundsViewModel PriceBounds { get; init; } = new();
}

public sealed class CatalogActiveFiltersViewModel
{
    public string? Search { get; init; }
    public string? Category { get; init; }
    public string? CategoryName { get; init; }
    public IReadOnlyList<string> Brands { get; init; } = Array.Empty<string>();
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public bool InStockOnly { get; init; }
    public bool DealsOnly { get; init; }
    public bool FeaturedOnly { get; init; }
    public string Sort { get; init; } = "featured";

    public bool HasAnyFilter =>
        !string.IsNullOrWhiteSpace(Search) ||
        !string.IsNullOrWhiteSpace(Category) ||
        Brands.Count > 0 ||
        MinPrice.HasValue ||
        MaxPrice.HasValue ||
        InStockOnly ||
        DealsOnly ||
        FeaturedOnly;

    public int ActiveCount =>
        (!string.IsNullOrWhiteSpace(Search) ? 1 : 0) +
        (!string.IsNullOrWhiteSpace(Category) ? 1 : 0) +
        Brands.Count +
        (MinPrice.HasValue || MaxPrice.HasValue ? 1 : 0) +
        (InStockOnly ? 1 : 0) +
        (DealsOnly ? 1 : 0) +
        (FeaturedOnly ? 1 : 0);
}

public sealed class CatalogSearchResultViewModel
{
    public IReadOnlyList<ProductCardViewModel> Items { get; init; } = Array.Empty<ProductCardViewModel>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public bool HasPrevious { get; init; }
    public bool HasNext { get; init; }
    public string Sort { get; init; } = "featured";
    public int LowStockThreshold { get; init; }
    public CatalogActiveFiltersViewModel ActiveFilters { get; init; } = new();
    public CatalogFacetsViewModel Facets { get; init; } = new();
}
