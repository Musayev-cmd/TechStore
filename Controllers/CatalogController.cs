using Microsoft.AspNetCore.Mvc;
using TechStore.Services;
using TechStore.Services.Localization;
using TechStore.ViewModels;

namespace TechStore.Controllers;

public class CatalogController : Controller
{
    private readonly IProductCatalogService _catalogService;

    public CatalogController(IProductCatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet("/Catalog")]
    [HttpGet("/Catalog/Index")]
    public async Task<IActionResult> Index(
        [FromQuery(Name = "q")] string? q,
        [FromQuery(Name = "search")] string? search,
        [FromQuery(Name = "category")] string? category,
        [FromQuery(Name = "brand")] string[]? brand,
        [FromQuery(Name = "minPrice")] decimal? minPrice,
        [FromQuery(Name = "maxPrice")] decimal? maxPrice,
        [FromQuery(Name = "inStock")] bool inStock = false,
        [FromQuery(Name = "deals")] bool deals = false,
        [FromQuery(Name = "featured")] bool featured = false,
        [FromQuery(Name = "sort")] string? sort = null,
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "pageSize")] int pageSize = CatalogQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var culture = LocalizationHelper.CurrentCultureCode();
        var effectiveSearch = !string.IsNullOrWhiteSpace(q) ? q : search;

        var query = new CatalogQuery
        {
            Search = effectiveSearch,
            Category = category,
            Brands = CatalogQuery.ParseBrands(null, brand),
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            InStockOnly = inStock,
            DealsOnly = deals,
            FeaturedOnly = featured,
            Sort = CatalogQuery.NormalizeSort(sort),
            Page = page,
            PageSize = pageSize
        };

        var result = await _catalogService.SearchAsync(query, culture, cancellationToken);
        return View(result);
    }

    [HttpGet("/Catalog/Details/{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return RedirectToAction(nameof(Index));
        }

        var culture = LocalizationHelper.CurrentCultureCode();
        var product = await _catalogService.GetBySlugAsync(slug, culture, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        ViewData["Title"] = product.Name;
        ViewData["Description"] = product.ShortDescription;

        return View(product);
    }
}
