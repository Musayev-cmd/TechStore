using TechStore.ViewModels;

namespace TechStore.Services;

public interface IProductCatalogService
{
    Task<HomeViewModel> GetHomePageDataAsync(string culture, CancellationToken cancellationToken = default);
    Task<CatalogSearchResultViewModel> SearchAsync(CatalogQuery query, string culture, CancellationToken cancellationToken = default);
    Task<CatalogFacetsViewModel> GetFacetsAsync(string culture, CancellationToken cancellationToken = default);
    Task<ProductDetailViewModel?> GetBySlugAsync(string slug, string culture, CancellationToken cancellationToken = default);
}
