using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using TechStore.Data;
using TechStore.Models;
using TechStore.Options;
using TechStore.Services;
using TechStore.Services.Localization;
using TechStore.ViewModels;

namespace TechStore.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = DbSeeder.AdminRole)]
public sealed class ProductsController : AdminBaseController
{
    private const int PageSize = 15;

    private readonly AppDbContext _db;
    private readonly IImageService _imageService;
    private readonly ICurrencyFormatter _currency;
    private readonly StoreOptions _storeOptions;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        AppDbContext db,
        IImageService imageService,
        ICurrencyFormatter currency,
        IOptions<StoreOptions> storeOptions,
        IStringLocalizer<SharedResource> localizer,
        ILogger<ProductsController> logger)
    {
        _db = db;
        _imageService = imageService;
        _currency = currency;
        _storeOptions = storeOptions.Value;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int? categoryId,
        string? stock,
        string? status,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var culture = LocalizationHelper.CurrentCultureCode();
        var lowStockThreshold = _storeOptions.LowStockThreshold;
        var normalizedStock = (stock ?? "all").Trim().ToLowerInvariant();
        var normalizedStatus = (status ?? "all").Trim().ToLowerInvariant();
        page = Math.Max(1, page);

        var query = _db.Products
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Specifications)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.Sku.ToLower().Contains(term) ||
                p.Brand.ToLower().Contains(term) ||
                p.Slug.ToLower().Contains(term) ||
                p.NameEn.ToLower().Contains(term) ||
                p.NameRu.ToLower().Contains(term) ||
                p.NameAz.ToLower().Contains(term));
        }

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        query = normalizedStock switch
        {
            "in-stock" => query.Where(p => p.StockQuantity > lowStockThreshold),
            "low-stock" => query.Where(p => p.StockQuantity > 0 && p.StockQuantity <= lowStockThreshold),
            "out-of-stock" => query.Where(p => p.StockQuantity <= 0),
            _ => query
        };

        query = normalizedStatus switch
        {
            "active" => query.Where(p => p.IsActive),
            "inactive" => query.Where(p => !p.IsActive),
            _ => query
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
        if (page > totalPages)
        {
            page = totalPages;
        }

        var entities = await query
            .OrderByDescending(p => p.UpdatedAtUtc)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        var categories = await LoadCategoryOptionsAsync(culture, cancellationToken);

        var rows = entities.Select(p =>
        {
            var primaryImg = p.Images
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.DisplayOrder)
                .FirstOrDefault()?.Url ?? string.Empty;

            return new AdminProductRowViewModel
            {
                Id = p.Id,
                Sku = p.Sku,
                Slug = p.Slug,
                Brand = p.Brand,
                Name = p.GetName(culture),
                CategoryName = p.Category?.GetName(culture) ?? string.Empty,
                FormattedPrice = _currency.Format(p.Price),
                FormattedDiscountPrice = p.HasDiscount ? _currency.Format(p.DiscountPrice!.Value) : null,
                StockQuantity = p.StockQuantity,
                IsOutOfStock = p.StockQuantity <= 0,
                IsLowStock = p.StockQuantity > 0 && p.StockQuantity <= lowStockThreshold,
                IsFeatured = p.IsFeatured,
                IsActive = p.IsActive,
                ImageCount = p.Images.Count,
                SpecCount = p.Specifications.Count,
                PrimaryImageUrl = primaryImg,
                UpdatedAtUtc = p.UpdatedAtUtc
            };
        }).ToList();

        ViewData["Title"] = _localizer["Admin.ProductsTitle"].Value;

        return View(new AdminProductListViewModel
        {
            Search = search?.Trim(),
            CategoryId = categoryId,
            StockFilter = normalizedStock,
            StatusFilter = normalizedStatus,
            Page = page,
            TotalPages = totalPages,
            TotalCount = totalCount,
            LowStockThreshold = lowStockThreshold,
            Categories = categories,
            Products = rows
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var culture = LocalizationHelper.CurrentCultureCode();
        ViewData["Title"] = _localizer["Admin.ProductCreateTitle"].Value;

        var model = new AdminProductFormViewModel
        {
            IsActive = true,
            StockQuantity = 10,
            Categories = await LoadCategoryOptionsAsync(culture, cancellationToken),
            Specifications = [new AdminProductSpecInputModel { DisplayOrder = 1 }]
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminProductFormViewModel model, CancellationToken cancellationToken)
    {
        var culture = LocalizationHelper.CurrentCultureCode();
        ViewData["Title"] = _localizer["Admin.ProductCreateTitle"].Value;

        var normalizedSlug = await ValidateProductFormAsync(model, excludeProductId: null, cancellationToken);
        var validSpecs = ValidateAndExtractSpecs(model.Specifications);

        if (!ModelState.IsValid)
        {
            model.Categories = await LoadCategoryOptionsAsync(culture, cancellationToken);
            EnsureAtLeastOneSpecRow(model);
            return View(model);
        }

        // Validate and save uploaded images only after all other checks succeed
        var savedImageUrls = new List<string>();
        if (model.NewImages is { Count: > 0 })
        {
            foreach (var file in model.NewImages.Where(f => f is not null && f.Length > 0))
            {
                var upload = await _imageService.SaveProductImageAsync(file, cancellationToken);
                if (!upload.Succeeded)
                {
                    CleanupUploadedImages(savedImageUrls);
                    var key = upload.ErrorResourceKey ?? "Admin.ImageErrorInvalidSignature";
                    ModelState.AddModelError(nameof(model.NewImages), _localizer[key].Value);
                    model.Categories = await LoadCategoryOptionsAsync(culture, cancellationToken);
                    EnsureAtLeastOneSpecRow(model);
                    return View(model);
                }

                savedImageUrls.Add(upload.RelativeUrl!);
            }
        }

        var now = DateTime.UtcNow;
        var product = new Product
        {
            Sku = model.Sku.Trim(),
            Slug = normalizedSlug,
            Brand = model.Brand.Trim(),
            CategoryId = model.CategoryId,
            NameEn = model.NameEn.Trim(),
            NameRu = model.NameRu.Trim(),
            NameAz = model.NameAz.Trim(),
            ShortDescriptionEn = model.ShortDescriptionEn.Trim(),
            ShortDescriptionRu = model.ShortDescriptionRu.Trim(),
            ShortDescriptionAz = model.ShortDescriptionAz.Trim(),
            DescriptionEn = model.DescriptionEn.Trim(),
            DescriptionRu = model.DescriptionRu.Trim(),
            DescriptionAz = model.DescriptionAz.Trim(),
            Price = model.Price,
            DiscountPrice = model.DiscountPrice,
            StockQuantity = model.StockQuantity,
            IsFeatured = model.IsFeatured,
            IsActive = model.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        for (var i = 0; i < validSpecs.Count; i++)
        {
            var spec = validSpecs[i];
            product.Specifications.Add(new ProductSpecification
            {
                KeyEn = spec.KeyEn.Trim(),
                KeyRu = spec.KeyRu.Trim(),
                KeyAz = spec.KeyAz.Trim(),
                Value = spec.Value.Trim(),
                DisplayOrder = i + 1
            });
        }

        for (var i = 0; i < savedImageUrls.Count; i++)
        {
            product.Images.Add(new ProductImage
            {
                Url = savedImageUrls[i],
                AltText = product.NameEn,
                IsPrimary = i == 0,
                DisplayOrder = i + 1
            });
        }

        try
        {
            _db.Products.Add(product);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            CleanupUploadedImages(savedImageUrls);
            _logger.LogWarning(ex, "Database error while creating product SKU {Sku}.", product.Sku);
            ModelState.AddModelError(string.Empty, _localizer["Admin.SaveFailedGeneric"].Value);
            model.Categories = await LoadCategoryOptionsAsync(culture, cancellationToken);
            EnsureAtLeastOneSpecRow(model);
            return View(model);
        }

        _logger.LogInformation("Admin {Admin} created product #{ProductId} ({Sku}).", User.Identity?.Name, product.Id, product.Sku);
        SetSuccessNotice(_localizer["Admin.ProductCreatedSuccess", product.GetName(culture)].Value);
        return RedirectToAction(nameof(Edit), new { id = product.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var culture = LocalizationHelper.CurrentCultureCode();
        var product = await _db.Products
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.Images)
            .Include(p => p.Specifications)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        ViewData["Title"] = _localizer["Admin.ProductEditTitle"].Value;

        var existingImages = product.Images
            .OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.DisplayOrder)
            .ThenBy(i => i.Id)
            .Select(i => new AdminProductImageItem
            {
                Id = i.Id,
                Url = i.Url,
                AltText = i.AltText,
                IsPrimary = i.IsPrimary,
                DisplayOrder = i.DisplayOrder
            })
            .ToList();

        var specs = product.Specifications
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Id)
            .Select(s => new AdminProductSpecInputModel
            {
                KeyEn = s.KeyEn,
                KeyRu = s.KeyRu,
                KeyAz = s.KeyAz,
                Value = s.Value,
                DisplayOrder = s.DisplayOrder
            })
            .ToList();

        var model = new AdminProductFormViewModel
        {
            Id = product.Id,
            Sku = product.Sku,
            Slug = product.Slug,
            Brand = product.Brand,
            CategoryId = product.CategoryId,
            NameEn = product.NameEn,
            NameRu = product.NameRu,
            NameAz = product.NameAz,
            ShortDescriptionEn = product.ShortDescriptionEn,
            ShortDescriptionRu = product.ShortDescriptionRu,
            ShortDescriptionAz = product.ShortDescriptionAz,
            DescriptionEn = product.DescriptionEn,
            DescriptionRu = product.DescriptionRu,
            DescriptionAz = product.DescriptionAz,
            Price = product.Price,
            DiscountPrice = product.DiscountPrice,
            StockQuantity = product.StockQuantity,
            IsFeatured = product.IsFeatured,
            IsActive = product.IsActive,
            Specifications = specs,
            ExistingImages = existingImages,
            PrimaryImageId = existingImages.FirstOrDefault(i => i.IsPrimary)?.Id ?? existingImages.FirstOrDefault()?.Id,
            Categories = await LoadCategoryOptionsAsync(culture, cancellationToken)
        };

        EnsureAtLeastOneSpecRow(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminProductFormViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var culture = LocalizationHelper.CurrentCultureCode();
        ViewData["Title"] = _localizer["Admin.ProductEditTitle"].Value;

        var product = await _db.Products
            .AsSplitQuery()
            .Include(p => p.Images)
            .Include(p => p.Specifications)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        var normalizedSlug = await ValidateProductFormAsync(model, excludeProductId: id, cancellationToken);
        var validSpecs = ValidateAndExtractSpecs(model.Specifications);

        if (!ModelState.IsValid)
        {
            await HydrateEditModelAsync(model, product, culture, cancellationToken);
            return View(model);
        }

        var savedNewImageUrls = new List<string>();
        if (model.NewImages is { Count: > 0 })
        {
            foreach (var file in model.NewImages.Where(f => f is not null && f.Length > 0))
            {
                var upload = await _imageService.SaveProductImageAsync(file, cancellationToken);
                if (!upload.Succeeded)
                {
                    CleanupUploadedImages(savedNewImageUrls);
                    var key = upload.ErrorResourceKey ?? "Admin.ImageErrorInvalidSignature";
                    ModelState.AddModelError(nameof(model.NewImages), _localizer[key].Value);
                    await HydrateEditModelAsync(model, product, culture, cancellationToken);
                    return View(model);
                }

                savedNewImageUrls.Add(upload.RelativeUrl!);
            }
        }

        product.Sku = model.Sku.Trim();
        product.Slug = normalizedSlug;
        product.Brand = model.Brand.Trim();
        product.CategoryId = model.CategoryId;
        product.NameEn = model.NameEn.Trim();
        product.NameRu = model.NameRu.Trim();
        product.NameAz = model.NameAz.Trim();
        product.ShortDescriptionEn = model.ShortDescriptionEn.Trim();
        product.ShortDescriptionRu = model.ShortDescriptionRu.Trim();
        product.ShortDescriptionAz = model.ShortDescriptionAz.Trim();
        product.DescriptionEn = model.DescriptionEn.Trim();
        product.DescriptionRu = model.DescriptionRu.Trim();
        product.DescriptionAz = model.DescriptionAz.Trim();
        product.Price = model.Price;
        product.DiscountPrice = model.DiscountPrice;
        product.StockQuantity = model.StockQuantity;
        product.IsFeatured = model.IsFeatured;
        product.IsActive = model.IsActive;
        product.UpdatedAtUtc = DateTime.UtcNow;

        // Replace specifications atomically
        _db.ProductSpecifications.RemoveRange(product.Specifications);
        product.Specifications.Clear();
        for (var i = 0; i < validSpecs.Count; i++)
        {
            var spec = validSpecs[i];
            product.Specifications.Add(new ProductSpecification
            {
                ProductId = product.Id,
                KeyEn = spec.KeyEn.Trim(),
                KeyRu = spec.KeyRu.Trim(),
                KeyAz = spec.KeyAz.Trim(),
                Value = spec.Value.Trim(),
                DisplayOrder = i + 1
            });
        }

        // Handle image removals (collect URLs and delete from disk only after DB commit)
        var removeIds = (model.RemoveImageIds ?? new List<int>()).ToHashSet();
        var removedImages = product.Images.Where(i => removeIds.Contains(i.Id)).ToList();
        var urlsToDeleteAfterCommit = removedImages.Select(i => i.Url).ToList();

        foreach (var img in removedImages)
        {
            product.Images.Remove(img);
            _db.ProductImages.Remove(img);
        }

        var nextDisplayOrder = product.Images.Count == 0
            ? 1
            : product.Images.Max(i => i.DisplayOrder) + 1;

        foreach (var url in savedNewImageUrls)
        {
            product.Images.Add(new ProductImage
            {
                ProductId = product.Id,
                Url = url,
                AltText = product.NameEn,
                IsPrimary = false,
                DisplayOrder = nextDisplayOrder++
            });
        }

        // Ensure primary image invariant across remaining + newly added images
        if (product.Images.Count > 0)
        {
            var targetPrimary = model.PrimaryImageId.HasValue
                ? product.Images.FirstOrDefault(i => i.Id == model.PrimaryImageId.Value)
                : null;

            targetPrimary ??= product.Images
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.DisplayOrder)
                .ThenBy(i => i.Id)
                .First();

            foreach (var img in product.Images)
            {
                img.IsPrimary = ReferenceEquals(img, targetPrimary);
            }
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            CleanupUploadedImages(savedNewImageUrls);
            _logger.LogWarning(ex, "Database error while updating product #{ProductId}.", id);
            ModelState.AddModelError(string.Empty, _localizer["Admin.SaveFailedGeneric"].Value);
            await HydrateEditModelAsync(model, product, culture, cancellationToken);
            return View(model);
        }

        CleanupUploadedImages(urlsToDeleteAfterCommit);

        _logger.LogInformation("Admin {Admin} updated product #{ProductId} ({Sku}).", User.Identity?.Name, product.Id, product.Sku);
        SetSuccessNotice(_localizer["Admin.ProductUpdatedSuccess", product.GetName(culture)].Value);
        return RedirectToAction(nameof(Edit), new { id = product.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var culture = LocalizationHelper.CurrentCultureCode();
        var product = await _db.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            SetErrorNotice(_localizer["Product.NotFound"].Value);
            return RedirectToAction(nameof(Index));
        }

        var displayName = product.GetName(culture);
        var sku = product.Sku;
        var imageUrls = product.Images.Select(i => i.Url).ToList();

        _db.Products.Remove(product);
        await _db.SaveChangesAsync(cancellationToken);

        CleanupUploadedImages(imageUrls);

        _logger.LogInformation("Admin {Admin} deleted product #{ProductId} ({Sku}).", User.Identity?.Name, id, sku);
        SetSuccessNotice(_localizer["Admin.ProductDeletedSuccess", displayName].Value);
        return RedirectToAction(nameof(Index));
    }

    private async Task<string> ValidateProductFormAsync(
        AdminProductFormViewModel model,
        int? excludeProductId,
        CancellationToken cancellationToken)
    {
        if (model.DiscountPrice.HasValue && model.DiscountPrice.Value >= model.Price)
        {
            ModelState.AddModelError(nameof(model.DiscountPrice), _localizer["Admin.ValidationDiscountLessThanPrice"].Value);
        }

        var categoryExists = await _db.Categories
            .AsNoTracking()
            .AnyAsync(c => c.Id == model.CategoryId, cancellationToken);

        if (!categoryExists)
        {
            ModelState.AddModelError(nameof(model.CategoryId), _localizer["Validation.Required"].Value);
        }

        var skuLower = (model.Sku ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(skuLower))
        {
            var skuTaken = await _db.Products
                .AsNoTracking()
                .AnyAsync(p => p.Sku.ToLower() == skuLower && (!excludeProductId.HasValue || p.Id != excludeProductId.Value), cancellationToken);

            if (skuTaken)
            {
                ModelState.AddModelError(nameof(model.Sku), _localizer["Admin.ValidationSkuDuplicate"].Value);
            }
        }

        var rawSlug = string.IsNullOrWhiteSpace(model.Slug) ? model.NameEn : model.Slug;
        var normalizedSlug = AdminHelpers.GenerateSlug(rawSlug, maxLength: 155);
        if (string.IsNullOrWhiteSpace(normalizedSlug))
        {
            ModelState.AddModelError(nameof(model.Slug), _localizer["Admin.ValidationSlugInvalid"].Value);
            return string.Empty;
        }

        var slugTaken = await _db.Products
            .AsNoTracking()
            .AnyAsync(p => p.Slug.ToLower() == normalizedSlug && (!excludeProductId.HasValue || p.Id != excludeProductId.Value), cancellationToken);

        if (slugTaken)
        {
            ModelState.AddModelError(nameof(model.Slug), _localizer["Admin.ValidationSlugDuplicate"].Value);
        }

        model.Slug = normalizedSlug;
        return normalizedSlug;
    }

    private List<AdminProductSpecInputModel> ValidateAndExtractSpecs(List<AdminProductSpecInputModel>? rawSpecs)
    {
        var result = new List<AdminProductSpecInputModel>();
        if (rawSpecs is null || rawSpecs.Count == 0)
        {
            return result;
        }

        var hasIncompleteRow = false;
        foreach (var spec in rawSpecs)
        {
            if (spec is null)
            {
                continue;
            }

            var hasAny = !string.IsNullOrWhiteSpace(spec.KeyEn) ||
                         !string.IsNullOrWhiteSpace(spec.KeyRu) ||
                         !string.IsNullOrWhiteSpace(spec.KeyAz) ||
                         !string.IsNullOrWhiteSpace(spec.Value);

            if (!hasAny)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(spec.KeyEn) ||
                string.IsNullOrWhiteSpace(spec.KeyRu) ||
                string.IsNullOrWhiteSpace(spec.KeyAz) ||
                string.IsNullOrWhiteSpace(spec.Value))
            {
                hasIncompleteRow = true;
                continue;
            }

            result.Add(spec);
        }

        if (hasIncompleteRow)
        {
            ModelState.AddModelError(nameof(AdminProductFormViewModel.Specifications), _localizer["Admin.SpecRowIncomplete"].Value);
        }

        return result;
    }

    private async Task<IReadOnlyList<AdminCategoryOption>> LoadCategoryOptionsAsync(
        string culture,
        CancellationToken cancellationToken)
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.NameEn)
            .ToListAsync(cancellationToken);

        return categories.Select(c => new AdminCategoryOption
        {
            Id = c.Id,
            Name = c.GetName(culture),
            IsActive = c.IsActive
        }).ToList();
    }

    private async Task HydrateEditModelAsync(
        AdminProductFormViewModel model,
        Product product,
        string culture,
        CancellationToken cancellationToken)
    {
        model.Categories = await LoadCategoryOptionsAsync(culture, cancellationToken);
        model.ExistingImages = product.Images
            .OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.DisplayOrder)
            .ThenBy(i => i.Id)
            .Select(i => new AdminProductImageItem
            {
                Id = i.Id,
                Url = i.Url,
                AltText = i.AltText,
                IsPrimary = i.IsPrimary,
                DisplayOrder = i.DisplayOrder
            })
            .ToList();
        EnsureAtLeastOneSpecRow(model);
    }

    private static void EnsureAtLeastOneSpecRow(AdminProductFormViewModel model)
    {
        if (model.Specifications is null || model.Specifications.Count == 0)
        {
            model.Specifications = [new AdminProductSpecInputModel { DisplayOrder = 1 }];
        }
    }

    private void CleanupUploadedImages(IEnumerable<string> urls)
    {
        foreach (var url in urls)
        {
            _imageService.DeleteProductImage(url);
        }
    }
}
