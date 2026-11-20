using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TechStore.Data;
using TechStore.Models;
using TechStore.Options;
using TechStore.Services.Localization;
using TechStore.ViewModels;

namespace TechStore.Services;

public sealed class ProductCatalogService : IProductCatalogService
{
    private readonly AppDbContext _db;
    private readonly ICurrencyFormatter _currency;
    private readonly StoreOptions _storeOptions;

    public ProductCatalogService(
        AppDbContext db,
        ICurrencyFormatter currency,
        IOptions<StoreOptions> storeOptions)
    {
        _db = db;
        _currency = currency;
        _storeOptions = storeOptions.Value;
    }

    public async Task<HomeViewModel> GetHomePageDataAsync(string culture, CancellationToken cancellationToken = default)
    {
        var normalizedCulture = JsonLocalizationStore.NormalizeCulture(culture);

        var categoryRows = await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new
            {
                Category = c,
                Count = c.Products.Count(p => p.IsActive)
            })
            .ToListAsync(cancellationToken);

        var activeProductsQuery = _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.Category != null && p.Category.IsActive);

        var totalProducts = await activeProductsQuery.CountAsync(cancellationToken);
        var inStockProducts = await activeProductsQuery.CountAsync(p => p.StockQuantity > 0, cancellationToken);

        var featuredEntities = await activeProductsQuery
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Where(p => p.IsFeatured)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ThenBy(p => p.Id)
            .Take(8)
            .ToListAsync(cancellationToken);

        var dealEntities = await activeProductsQuery
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Where(p => p.DiscountPrice.HasValue && p.DiscountPrice.Value > 0m && p.DiscountPrice.Value < p.Price)
            .OrderByDescending(p => p.IsFeatured)
            .ThenByDescending(p => p.CreatedAtUtc)
            .Take(4)
            .ToListAsync(cancellationToken);

        var brandRows = await activeProductsQuery
            .GroupBy(p => p.Brand)
            .Select(g => new BrandFacetViewModel
            {
                Name = g.Key,
                ProductCount = g.Count()
            })
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);

        var categories = categoryRows.Select(item => new CategoryFacetViewModel
        {
            Id = item.Category.Id,
            Slug = item.Category.Slug,
            Name = item.Category.GetName(normalizedCulture),
            Description = item.Category.GetDescription(normalizedCulture),
            IconName = item.Category.IconName,
            ProductCount = item.Count
        }).ToList();

        var featuredCards = featuredEntities
            .Select(p => MapToCard(p, normalizedCulture))
            .ToList();

        var dealCards = dealEntities
            .Select(p => MapToCard(p, normalizedCulture))
            .ToList();

        return new HomeViewModel
        {
            TotalCategories = categories.Count,
            TotalProducts = totalProducts,
            InStockProducts = inStockProducts,
            LowStockThreshold = _storeOptions.LowStockThreshold,
            FreeShippingThreshold = _storeOptions.FreeShippingThreshold,
            FormattedFreeShippingThreshold = _currency.Format(_storeOptions.FreeShippingThreshold),
            HeroProduct = featuredCards.FirstOrDefault(),
            Categories = categories,
            FeaturedProducts = featuredCards,
            DealProducts = dealCards,
            Brands = brandRows
        };
    }

    public async Task<CatalogFacetsViewModel> GetFacetsAsync(string culture, CancellationToken cancellationToken = default)
    {
        var normalizedCulture = JsonLocalizationStore.NormalizeCulture(culture);

        var activeCategories = await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new
            {
                Category = c,
                Count = c.Products.Count(p => p.IsActive)
            })
            .ToListAsync(cancellationToken);

        var activeProducts = _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.Category != null && p.Category.IsActive);

        var brands = await activeProducts
            .GroupBy(p => p.Brand)
            .Select(g => new BrandFacetViewModel
            {
                Name = g.Key,
                ProductCount = g.Count()
            })
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);

        var prices = await activeProducts
            .Select(p => new { p.Price, p.DiscountPrice })
            .ToListAsync(cancellationToken);

        var priceBounds = ComputePriceBounds(prices.Select(x => ComputeEffectivePrice(x.Price, x.DiscountPrice)));

        return new CatalogFacetsViewModel
        {
            Categories = activeCategories.Select(x => new CategoryFacetViewModel
            {
                Id = x.Category.Id,
                Slug = x.Category.Slug,
                Name = x.Category.GetName(normalizedCulture),
                Description = x.Category.GetDescription(normalizedCulture),
                IconName = x.Category.IconName,
                ProductCount = x.Count
            }).ToList(),
            Brands = brands,
            PriceBounds = priceBounds
        };
    }

    public async Task<CatalogSearchResultViewModel> SearchAsync(
        CatalogQuery query,
        string culture,
        CancellationToken cancellationToken = default)
    {
        var normalizedCulture = JsonLocalizationStore.NormalizeCulture(culture);
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var categorySlug = string.IsNullOrWhiteSpace(query.Category) ? null : query.Category.Trim().ToLowerInvariant();
        var selectedBrands = query.Brands
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Select(b => b.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var minPrice = query.MinPrice is >= 0m ? query.MinPrice : null;
        var maxPrice = query.MaxPrice is >= 0m ? query.MaxPrice : null;
        if (minPrice.HasValue && maxPrice.HasValue && minPrice.Value > maxPrice.Value)
        {
            (minPrice, maxPrice) = (maxPrice, minPrice);
        }

        var sort = CatalogQuery.NormalizeSort(query.Sort);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? CatalogQuery.DefaultPageSize : query.PageSize, 1, CatalogQuery.MaxPageSize);
        var requestedPage = Math.Max(1, query.Page);

        // Fetch active categories for facet metadata & localized names
        var allCategories = await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(cancellationToken);

        // SQLite LOWER()/LIKE is ASCII-only; handle non-ASCII Cyrillic/Azerbaijani search with full Unicode OrdinalIgnoreCase matching.
        var requiresInMemorySearch = !string.IsNullOrEmpty(search) && _db.Database.IsSqlite() && !IsAsciiOnly(search);

        var baseQuery = _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Where(p => p.IsActive && p.Category != null && p.Category.IsActive);

        // Build base filtered query for disjunctive facets (shared filters: search, price, inStock, deals, featured)
        var commonQuery = ApplyCommonFilters(baseQuery, search, minPrice, maxPrice, query.InStockOnly, query.DealsOnly, query.FeaturedOnly, requiresInMemorySearch);

        List<Product> pagedProducts;
        int totalCount;
        int totalPages;
        int currentPage;
        Dictionary<int, int> categoryCounts;
        List<BrandFacetViewModel> brandFacets;
        PriceBoundsViewModel priceBounds;

        if (requiresInMemorySearch)
        {
            var candidates = await commonQuery.ToListAsync(cancellationToken);
            var searchMatched = candidates.Where(p => MatchesSearch(p, search!)).ToList();

            var lowerBrands = selectedBrands.Select(b => b.ToLowerInvariant()).ToHashSet();

            // Category counts respect all active filters except category
            var forCategoryFacets = lowerBrands.Count > 0
                ? searchMatched.Where(p => lowerBrands.Contains(p.Brand.ToLowerInvariant()))
                : searchMatched;

            categoryCounts = forCategoryFacets
                .GroupBy(p => p.CategoryId)
                .ToDictionary(g => g.Key, g => g.Count());

            // Brand counts respect all active filters except brand
            var forBrandFacets = !string.IsNullOrEmpty(categorySlug)
                ? searchMatched.Where(p => p.Category != null && p.Category.Slug.Equals(categorySlug, StringComparison.OrdinalIgnoreCase))
                : searchMatched;

            brandFacets = BuildBrandFacets(forBrandFacets.Select(p => p.Brand), selectedBrands);

            // Apply category and brand filters for actual results
            IEnumerable<Product> filtered = searchMatched;
            if (!string.IsNullOrEmpty(categorySlug))
            {
                filtered = filtered.Where(p => p.Category != null && p.Category.Slug.Equals(categorySlug, StringComparison.OrdinalIgnoreCase));
            }
            if (lowerBrands.Count > 0)
            {
                filtered = filtered.Where(p => lowerBrands.Contains(p.Brand.ToLowerInvariant()));
            }

            var ordered = ApplyInMemorySort(filtered, sort, normalizedCulture).ToList();
            totalCount = ordered.Count;
            totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
            currentPage = Math.Min(requestedPage, totalPages);
            pagedProducts = ordered.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();
        }
        else
        {
            var lowerBrands = selectedBrands.Select(b => b.ToLowerInvariant()).ToList();

            // Category counts (disjunctive: exclude category filter)
            var categoryFacetQuery = commonQuery;
            if (lowerBrands.Count > 0)
            {
                categoryFacetQuery = categoryFacetQuery.Where(p => lowerBrands.Contains(p.Brand.ToLower()));
            }

            categoryCounts = await categoryFacetQuery
                .GroupBy(p => p.CategoryId)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CategoryId, x => x.Count, cancellationToken);

            // Brand counts (disjunctive: exclude brand filter)
            var brandFacetQuery = commonQuery;
            if (!string.IsNullOrEmpty(categorySlug))
            {
                brandFacetQuery = brandFacetQuery.Where(p => p.Category != null && p.Category.Slug == categorySlug);
            }

            var rawBrands = await brandFacetQuery
                .GroupBy(p => p.Brand)
                .Select(g => new BrandFacetViewModel
                {
                    Name = g.Key,
                    ProductCount = g.Count()
                })
                .OrderBy(b => b.Name)
                .ToListAsync(cancellationToken);

            brandFacets = EnsureSelectedBrandsPresent(rawBrands, selectedBrands);

            // Apply category & brand filters to the final result query
            var filteredQuery = commonQuery;
            if (!string.IsNullOrEmpty(categorySlug))
            {
                filteredQuery = filteredQuery.Where(p => p.Category != null && p.Category.Slug == categorySlug);
            }

            if (lowerBrands.Count > 0)
            {
                filteredQuery = filteredQuery.Where(p => lowerBrands.Contains(p.Brand.ToLower()));
            }

            totalCount = await filteredQuery.CountAsync(cancellationToken);
            totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
            currentPage = Math.Min(requestedPage, totalPages);

            var sortedQuery = ApplySqlSort(filteredQuery, sort, normalizedCulture);
            pagedProducts = await sortedQuery
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }

        // Price bounds for the active catalog (scoped to category if selected, otherwise entire active catalog)
        var boundsSourceQuery = _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.Category != null && p.Category.IsActive);

        if (!string.IsNullOrEmpty(categorySlug))
        {
            boundsSourceQuery = boundsSourceQuery.Where(p => p.Category != null && p.Category.Slug == categorySlug);
        }

        var boundPrices = await boundsSourceQuery
            .Select(p => new { p.Price, p.DiscountPrice })
            .ToListAsync(cancellationToken);

        priceBounds = ComputePriceBounds(boundPrices.Select(x => ComputeEffectivePrice(x.Price, x.DiscountPrice)));

        var categoryFacets = allCategories.Select(c => new CategoryFacetViewModel
        {
            Id = c.Id,
            Slug = c.Slug,
            Name = c.GetName(normalizedCulture),
            Description = c.GetDescription(normalizedCulture),
            IconName = c.IconName,
            ProductCount = categoryCounts.GetValueOrDefault(c.Id, 0)
        }).ToList();

        var activeCategoryName = string.IsNullOrEmpty(categorySlug)
            ? null
            : categoryFacets.FirstOrDefault(c => c.Slug.Equals(categorySlug, StringComparison.OrdinalIgnoreCase))?.Name;

        // Normalize canonical brand names from facets if matched case-insensitively
        var canonicalBrands = selectedBrands
            .Select(b => brandFacets.FirstOrDefault(f => f.Name.Equals(b, StringComparison.OrdinalIgnoreCase))?.Name ?? b)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CatalogSearchResultViewModel
        {
            Items = pagedProducts.Select(p => MapToCard(p, normalizedCulture)).ToList(),
            TotalCount = totalCount,
            Page = currentPage,
            PageSize = pageSize,
            TotalPages = totalPages,
            HasPrevious = currentPage > 1,
            HasNext = currentPage < totalPages,
            Sort = sort,
            LowStockThreshold = _storeOptions.LowStockThreshold,
            ActiveFilters = new CatalogActiveFiltersViewModel
            {
                Search = search,
                Category = categorySlug,
                CategoryName = activeCategoryName,
                Brands = canonicalBrands,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                InStockOnly = query.InStockOnly,
                DealsOnly = query.DealsOnly,
                FeaturedOnly = query.FeaturedOnly,
                Sort = sort
            },
            Facets = new CatalogFacetsViewModel
            {
                Categories = categoryFacets,
                Brands = brandFacets,
                PriceBounds = priceBounds
            }
        };
    }

    public async Task<ProductDetailViewModel?> GetBySlugAsync(
        string slug,
        string culture,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var normalizedCulture = JsonLocalizationStore.NormalizeCulture(culture);

        var product = await _db.Products
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Specifications)
            .FirstOrDefaultAsync(
                p => p.IsActive &&
                     p.Category != null &&
                     p.Category.IsActive &&
                     p.Slug.ToLower() == normalizedSlug,
                cancellationToken);

        if (product is null)
        {
            return null;
        }

        var localizedName = product.GetName(normalizedCulture);

        var images = product.Images
            .OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.DisplayOrder)
            .ThenBy(i => i.Id)
            .Select(i => new ProductImageViewModel
            {
                Id = i.Id,
                Url = i.Url,
                AltText = string.IsNullOrWhiteSpace(i.AltText) ? localizedName : i.AltText,
                IsPrimary = i.IsPrimary,
                DisplayOrder = i.DisplayOrder
            })
            .ToList();

        var specs = product.Specifications
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Id)
            .Select(s => new ProductSpecificationViewModel
            {
                Id = s.Id,
                Key = s.GetKey(normalizedCulture),
                Value = s.Value,
                DisplayOrder = s.DisplayOrder
            })
            .ToList();

        // Related products: prioritize same category, then backfill with same brand / featured up to 4 items
        var relatedEntities = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Where(p => p.IsActive &&
                        p.Category != null &&
                        p.Category.IsActive &&
                        p.Id != product.Id &&
                        p.CategoryId == product.CategoryId)
            .OrderByDescending(p => p.StockQuantity > 0)
            .ThenByDescending(p => p.IsFeatured)
            .ThenByDescending(p => p.CreatedAtUtc)
            .Take(4)
            .ToListAsync(cancellationToken);

        if (relatedEntities.Count < 4)
        {
            var excludeIds = relatedEntities.Select(p => p.Id).Append(product.Id).ToList();
            var needed = 4 - relatedEntities.Count;

            var backfill = await _db.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Where(p => p.IsActive &&
                            p.Category != null &&
                            p.Category.IsActive &&
                            !excludeIds.Contains(p.Id))
                .OrderByDescending(p => p.Brand == product.Brand)
                .ThenByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.StockQuantity > 0)
                .ThenByDescending(p => p.CreatedAtUtc)
                .Take(needed)
                .ToListAsync(cancellationToken);

            relatedEntities.AddRange(backfill);
        }

        var effectivePrice = product.EffectivePrice;
        var savings = product.HasDiscount ? product.Price - effectivePrice : 0m;

        return new ProductDetailViewModel
        {
            Id = product.Id,
            Sku = product.Sku,
            Slug = product.Slug,
            Brand = product.Brand,
            CategoryId = product.CategoryId,
            CategorySlug = product.Category?.Slug ?? string.Empty,
            CategoryName = product.Category?.GetName(normalizedCulture) ?? string.Empty,
            Name = localizedName,
            ShortDescription = product.GetShortDescription(normalizedCulture),
            Description = product.GetDescription(normalizedCulture),
            Price = product.Price,
            DiscountPrice = product.DiscountPrice,
            EffectivePrice = effectivePrice,
            FormattedPrice = _currency.Format(product.Price),
            FormattedEffectivePrice = _currency.Format(effectivePrice),
            SavingsAmount = savings,
            FormattedSavingsAmount = _currency.Format(savings),
            HasDiscount = product.HasDiscount,
            DiscountPercent = product.DiscountPercent,
            StockQuantity = product.StockQuantity,
            LowStockThreshold = _storeOptions.LowStockThreshold,
            StockState = GetStockState(product.StockQuantity),
            IsFeatured = product.IsFeatured,
            QualifiesForFreeShipping = effectivePrice >= _storeOptions.FreeShippingThreshold,
            FreeShippingThreshold = _storeOptions.FreeShippingThreshold,
            FormattedFreeShippingThreshold = _currency.Format(_storeOptions.FreeShippingThreshold),
            FlatShippingFee = _storeOptions.FlatShippingFee,
            FormattedFlatShippingFee = _currency.Format(_storeOptions.FlatShippingFee),
            Images = images,
            Specifications = specs,
            RelatedProducts = relatedEntities.Select(p => MapToCard(p, normalizedCulture)).ToList()
        };
    }

    private IQueryable<Product> ApplyCommonFilters(
        IQueryable<Product> query,
        string? search,
        decimal? minPrice,
        decimal? maxPrice,
        bool inStockOnly,
        bool dealsOnly,
        bool featuredOnly,
        bool deferSearchToMemory)
    {
        if (!string.IsNullOrEmpty(search) && !deferSearchToMemory)
        {
            var term = search.ToLowerInvariant();
            query = query.Where(p =>
                p.NameEn.ToLower().Contains(term) ||
                p.NameRu.ToLower().Contains(term) ||
                p.NameAz.ToLower().Contains(term) ||
                p.ShortDescriptionEn.ToLower().Contains(term) ||
                p.ShortDescriptionRu.ToLower().Contains(term) ||
                p.ShortDescriptionAz.ToLower().Contains(term) ||
                p.Brand.ToLower().Contains(term) ||
                p.Sku.ToLower().Contains(term) ||
                (p.Category != null && (
                    p.Category.NameEn.ToLower().Contains(term) ||
                    p.Category.NameRu.ToLower().Contains(term) ||
                    p.Category.NameAz.ToLower().Contains(term))));
        }

        if (minPrice.HasValue)
        {
            var min = minPrice.Value;
            query = query.Where(p =>
                (p.DiscountPrice.HasValue && p.DiscountPrice.Value > 0m && p.DiscountPrice.Value < p.Price
                    ? p.DiscountPrice.Value
                    : p.Price) >= min);
        }

        if (maxPrice.HasValue)
        {
            var max = maxPrice.Value;
            query = query.Where(p =>
                (p.DiscountPrice.HasValue && p.DiscountPrice.Value > 0m && p.DiscountPrice.Value < p.Price
                    ? p.DiscountPrice.Value
                    : p.Price) <= max);
        }

        if (inStockOnly)
        {
            query = query.Where(p => p.StockQuantity > 0);
        }

        if (dealsOnly)
        {
            query = query.Where(p => p.DiscountPrice.HasValue && p.DiscountPrice.Value > 0m && p.DiscountPrice.Value < p.Price);
        }

        if (featuredOnly)
        {
            query = query.Where(p => p.IsFeatured);
        }

        return query;
    }

    private static IQueryable<Product> ApplySqlSort(IQueryable<Product> query, string sort, string culture) =>
        sort switch
        {
            "price_asc" => query
                .OrderBy(p => p.DiscountPrice.HasValue && p.DiscountPrice.Value > 0m && p.DiscountPrice.Value < p.Price
                    ? p.DiscountPrice.Value
                    : p.Price)
                .ThenBy(p => p.Id),
            "price_desc" => query
                .OrderByDescending(p => p.DiscountPrice.HasValue && p.DiscountPrice.Value > 0m && p.DiscountPrice.Value < p.Price
                    ? p.DiscountPrice.Value
                    : p.Price)
                .ThenBy(p => p.Id),
            "newest" => query
                .OrderByDescending(p => p.CreatedAtUtc)
                .ThenByDescending(p => p.Id),
            "name_asc" => culture switch
            {
                "ru" => query.OrderBy(p => p.NameRu).ThenBy(p => p.Id),
                "az" => query.OrderBy(p => p.NameAz).ThenBy(p => p.Id),
                _ => query.OrderBy(p => p.NameEn).ThenBy(p => p.Id)
            },
            _ => query
                .OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.CreatedAtUtc)
                .ThenBy(p => p.Id)
        };

    private static IEnumerable<Product> ApplyInMemorySort(IEnumerable<Product> products, string sort, string culture) =>
        sort switch
        {
            "price_asc" => products.OrderBy(p => p.EffectivePrice).ThenBy(p => p.Id),
            "price_desc" => products.OrderByDescending(p => p.EffectivePrice).ThenBy(p => p.Id),
            "newest" => products.OrderByDescending(p => p.CreatedAtUtc).ThenByDescending(p => p.Id),
            "name_asc" => products.OrderBy(p => p.GetName(culture), StringComparer.CurrentCultureIgnoreCase).ThenBy(p => p.Id),
            _ => products.OrderByDescending(p => p.IsFeatured).ThenByDescending(p => p.CreatedAtUtc).ThenBy(p => p.Id)
        };

    private static bool MatchesSearch(Product p, string term)
    {
        return ContainsIgnoreCase(p.NameEn, term) ||
               ContainsIgnoreCase(p.NameRu, term) ||
               ContainsIgnoreCase(p.NameAz, term) ||
               ContainsIgnoreCase(p.ShortDescriptionEn, term) ||
               ContainsIgnoreCase(p.ShortDescriptionRu, term) ||
               ContainsIgnoreCase(p.ShortDescriptionAz, term) ||
               ContainsIgnoreCase(p.Brand, term) ||
               ContainsIgnoreCase(p.Sku, term) ||
               (p.Category is not null && (
                   ContainsIgnoreCase(p.Category.NameEn, term) ||
                   ContainsIgnoreCase(p.Category.NameRu, term) ||
                   ContainsIgnoreCase(p.Category.NameAz, term)));
    }

    private static bool ContainsIgnoreCase(string? source, string value) =>
        !string.IsNullOrEmpty(source) && source.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static bool IsAsciiOnly(string value)
    {
        foreach (var ch in value)
        {
            if (ch > 127)
            {
                return false;
            }
        }

        return true;
    }

    private static List<BrandFacetViewModel> BuildBrandFacets(IEnumerable<string> brands, IReadOnlyList<string> selectedBrands)
    {
        var grouped = brands
            .GroupBy(b => b, StringComparer.OrdinalIgnoreCase)
            .Select(g => new BrandFacetViewModel
            {
                Name = g.First(),
                ProductCount = g.Count()
            })
            .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return EnsureSelectedBrandsPresent(grouped, selectedBrands);
    }

    private static List<BrandFacetViewModel> EnsureSelectedBrandsPresent(
        List<BrandFacetViewModel> facets,
        IReadOnlyList<string> selectedBrands)
    {
        foreach (var selected in selectedBrands)
        {
            if (!facets.Any(f => f.Name.Equals(selected, StringComparison.OrdinalIgnoreCase)))
            {
                facets.Add(new BrandFacetViewModel { Name = selected, ProductCount = 0 });
            }
        }

        return facets.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static decimal ComputeEffectivePrice(decimal price, decimal? discountPrice) =>
        discountPrice.HasValue && discountPrice.Value > 0m && discountPrice.Value < price
            ? discountPrice.Value
            : price;

    private static PriceBoundsViewModel ComputePriceBounds(IEnumerable<decimal> prices)
    {
        var list = prices.ToList();
        if (list.Count == 0)
        {
            return new PriceBoundsViewModel { Min = 0m, Max = 0m };
        }

        return new PriceBoundsViewModel
        {
            Min = Math.Floor(list.Min()),
            Max = Math.Ceiling(list.Max())
        };
    }

    private ProductCardViewModel MapToCard(Product product, string culture)
    {
        var localizedName = product.GetName(culture);
        var primaryImage = product.Images
            .OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.DisplayOrder)
            .ThenBy(i => i.Id)
            .FirstOrDefault();

        var effectivePrice = product.EffectivePrice;

        return new ProductCardViewModel
        {
            Id = product.Id,
            Sku = product.Sku,
            Slug = product.Slug,
            Brand = product.Brand,
            CategorySlug = product.Category?.Slug ?? string.Empty,
            CategoryName = product.Category?.GetName(culture) ?? string.Empty,
            Name = localizedName,
            ShortDescription = product.GetShortDescription(culture),
            Price = product.Price,
            DiscountPrice = product.DiscountPrice,
            EffectivePrice = effectivePrice,
            FormattedPrice = _currency.Format(product.Price),
            FormattedEffectivePrice = _currency.Format(effectivePrice),
            HasDiscount = product.HasDiscount,
            DiscountPercent = product.DiscountPercent,
            StockQuantity = product.StockQuantity,
            StockState = GetStockState(product.StockQuantity),
            IsFeatured = product.IsFeatured,
            ImageUrl = primaryImage?.Url ?? string.Empty,
            ImageAlt = string.IsNullOrWhiteSpace(primaryImage?.AltText) ? localizedName : primaryImage.AltText,
            Url = $"/Catalog/Details/{product.Slug}"
        };
    }

    private string GetStockState(int stockQuantity)
    {
        if (stockQuantity <= 0)
        {
            return "out_of_stock";
        }

        if (stockQuantity <= _storeOptions.LowStockThreshold)
        {
            return "low_stock";
        }

        return "in_stock";
    }
}
