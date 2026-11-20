using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using TechStore.Data;
using TechStore.Models;
using TechStore.Services;
using TechStore.Services.Localization;
using TechStore.ViewModels;

namespace TechStore.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = DbSeeder.AdminRole)]
public sealed class CategoriesController : AdminBaseController
{
    private readonly AppDbContext _db;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<CategoriesController> _logger;

    public CategoriesController(
        AppDbContext db,
        IStringLocalizer<SharedResource> localizer,
        ILogger<CategoriesController> logger)
    {
        _db = db;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var culture = LocalizationHelper.CurrentCultureCode();
        ViewData["Title"] = _localizer["Admin.CategoriesTitle"].Value;

        var rows = await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Id)
            .Select(c => new
            {
                Category = c,
                ProductCount = c.Products.Count()
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(x => new AdminCategoryRowViewModel
        {
            Id = x.Category.Id,
            Slug = x.Category.Slug,
            LocalizedName = x.Category.GetName(culture),
            NameEn = x.Category.NameEn,
            NameRu = x.Category.NameRu,
            NameAz = x.Category.NameAz,
            IconName = x.Category.IconName,
            DisplayOrder = x.Category.DisplayOrder,
            IsActive = x.Category.IsActive,
            ProductCount = x.ProductCount
        }).ToList();

        return View(new AdminCategoryListViewModel { Categories = items });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        ViewData["Title"] = _localizer["Admin.CategoryCreateTitle"].Value;

        var nextOrder = (await _db.Categories
            .AsNoTracking()
            .Select(c => (int?)c.DisplayOrder)
            .MaxAsync(cancellationToken) ?? 0) + 1;

        return View(new AdminCategoryFormViewModel
        {
            IconName = "cpu",
            DisplayOrder = nextOrder,
            IsActive = true
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminCategoryFormViewModel model, CancellationToken cancellationToken)
    {
        var culture = LocalizationHelper.CurrentCultureCode();
        ViewData["Title"] = _localizer["Admin.CategoryCreateTitle"].Value;

        var normalizedSlug = await ValidateCategoryFormAsync(model, excludeCategoryId: null, cancellationToken);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var category = new Category
        {
            Slug = normalizedSlug,
            NameEn = model.NameEn.Trim(),
            NameRu = model.NameRu.Trim(),
            NameAz = model.NameAz.Trim(),
            DescriptionEn = string.IsNullOrWhiteSpace(model.DescriptionEn) ? null : model.DescriptionEn.Trim(),
            DescriptionRu = string.IsNullOrWhiteSpace(model.DescriptionRu) ? null : model.DescriptionRu.Trim(),
            DescriptionAz = string.IsNullOrWhiteSpace(model.DescriptionAz) ? null : model.DescriptionAz.Trim(),
            IconName = model.IconName.Trim().ToLowerInvariant(),
            DisplayOrder = model.DisplayOrder,
            IsActive = model.IsActive
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin {Admin} created category #{CategoryId} ({Slug}).", User.Identity?.Name, category.Id, category.Slug);
        SetSuccessNotice(_localizer["Admin.CategoryCreatedSuccess", category.GetName(culture)].Value);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var category = await _db.Categories
            .AsNoTracking()
            .Select(c => new
            {
                Category = c,
                ProductCount = c.Products.Count()
            })
            .FirstOrDefaultAsync(x => x.Category.Id == id, cancellationToken);

        if (category is null)
        {
            return NotFound();
        }

        ViewData["Title"] = _localizer["Admin.CategoryEditTitle"].Value;

        return View(new AdminCategoryFormViewModel
        {
            Id = category.Category.Id,
            Slug = category.Category.Slug,
            NameEn = category.Category.NameEn,
            NameRu = category.Category.NameRu,
            NameAz = category.Category.NameAz,
            DescriptionEn = category.Category.DescriptionEn,
            DescriptionRu = category.Category.DescriptionRu,
            DescriptionAz = category.Category.DescriptionAz,
            IconName = category.Category.IconName,
            DisplayOrder = category.Category.DisplayOrder,
            IsActive = category.Category.IsActive,
            ProductCount = category.ProductCount
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminCategoryFormViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var culture = LocalizationHelper.CurrentCultureCode();
        ViewData["Title"] = _localizer["Admin.CategoryEditTitle"].Value;

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        var normalizedSlug = await ValidateCategoryFormAsync(model, excludeCategoryId: id, cancellationToken);
        if (!ModelState.IsValid)
        {
            model.ProductCount = await _db.Products.AsNoTracking().CountAsync(p => p.CategoryId == id, cancellationToken);
            return View(model);
        }

        category.Slug = normalizedSlug;
        category.NameEn = model.NameEn.Trim();
        category.NameRu = model.NameRu.Trim();
        category.NameAz = model.NameAz.Trim();
        category.DescriptionEn = string.IsNullOrWhiteSpace(model.DescriptionEn) ? null : model.DescriptionEn.Trim();
        category.DescriptionRu = string.IsNullOrWhiteSpace(model.DescriptionRu) ? null : model.DescriptionRu.Trim();
        category.DescriptionAz = string.IsNullOrWhiteSpace(model.DescriptionAz) ? null : model.DescriptionAz.Trim();
        category.IconName = model.IconName.Trim().ToLowerInvariant();
        category.DisplayOrder = model.DisplayOrder;
        category.IsActive = model.IsActive;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin {Admin} updated category #{CategoryId} ({Slug}).", User.Identity?.Name, category.Id, category.Slug);
        SetSuccessNotice(_localizer["Admin.CategoryUpdatedSuccess", category.GetName(culture)].Value);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var culture = LocalizationHelper.CurrentCultureCode();
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category is null)
        {
            SetErrorNotice(_localizer["Admin.CategoryNotFound"].Value);
            return RedirectToAction(nameof(Index));
        }

        var productCount = await _db.Products
            .AsNoTracking()
            .CountAsync(p => p.CategoryId == id, cancellationToken);

        if (productCount > 0)
        {
            SetErrorNotice(_localizer["Admin.CategoryDeleteHasProducts", category.GetName(culture), productCount].Value);
            return RedirectToAction(nameof(Index));
        }

        var displayName = category.GetName(culture);
        var slug = category.Slug;

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin {Admin} deleted category #{CategoryId} ({Slug}).", User.Identity?.Name, id, slug);
        SetSuccessNotice(_localizer["Admin.CategoryDeletedSuccess", displayName].Value);
        return RedirectToAction(nameof(Index));
    }

    private async Task<string> ValidateCategoryFormAsync(
        AdminCategoryFormViewModel model,
        int? excludeCategoryId,
        CancellationToken cancellationToken)
    {
        if (!AdminHelpers.IsValidCategoryIcon(model.IconName))
        {
            ModelState.AddModelError(nameof(model.IconName), _localizer["Admin.ValidationInvalidIcon"].Value);
        }

        var rawSlug = string.IsNullOrWhiteSpace(model.Slug) ? model.NameEn : model.Slug;
        var normalizedSlug = AdminHelpers.GenerateSlug(rawSlug, maxLength: 95);
        if (string.IsNullOrWhiteSpace(normalizedSlug))
        {
            ModelState.AddModelError(nameof(model.Slug), _localizer["Admin.ValidationSlugInvalid"].Value);
            return string.Empty;
        }

        var slugTaken = await _db.Categories
            .AsNoTracking()
            .AnyAsync(c => c.Slug.ToLower() == normalizedSlug && (!excludeCategoryId.HasValue || c.Id != excludeCategoryId.Value), cancellationToken);

        if (slugTaken)
        {
            ModelState.AddModelError(nameof(model.Slug), _localizer["Admin.ValidationSlugDuplicate"].Value);
        }

        model.Slug = normalizedSlug;
        return normalizedSlug;
    }
}
