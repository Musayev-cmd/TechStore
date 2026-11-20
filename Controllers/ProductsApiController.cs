using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using TechStore.Services;
using TechStore.Services.Localization;
using TechStore.ViewModels;

namespace TechStore.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsApiController : ControllerBase
{
    private readonly IProductCatalogService _catalogService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ProductsApiController(
        IProductCatalogService catalogService,
        IStringLocalizer<SharedResource> localizer)
    {
        _catalogService = catalogService;
        _localizer = localizer;
    }

    [HttpGet]
    [ProducesResponseType(typeof(CatalogSearchResultViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProducts(
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
        return Ok(result);
    }

    [HttpGet("facets")]
    [ProducesResponseType(typeof(CatalogFacetsViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFacets(CancellationToken cancellationToken = default)
    {
        var culture = LocalizationHelper.CurrentCultureCode();
        var facets = await _catalogService.GetFacetsAsync(culture, cancellationToken);
        return Ok(facets);
    }

    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(ProductDetailViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductBySlug(string slug, CancellationToken cancellationToken = default)
    {
        var culture = LocalizationHelper.CurrentCultureCode();
        var product = await _catalogService.GetBySlugAsync(slug, culture, cancellationToken);
        if (product is null)
        {
            return NotFound(new { error = _localizer["Product.NotFound"].Value });
        }

        return Ok(product);
    }
}
