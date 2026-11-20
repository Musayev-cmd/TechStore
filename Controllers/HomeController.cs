using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TechStore.Data;
using TechStore.Options;
using TechStore.Services.Localization;
using TechStore.ViewModels;

namespace TechStore.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    private readonly StoreOptions _storeOptions;

    public HomeController(AppDbContext db, IOptions<StoreOptions> storeOptions)
    {
        _db = db;
        _storeOptions = storeOptions.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var culture = LocalizationHelper.CurrentCultureCode();

        var categories = await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new
            {
                Category = c,
                Count = c.Products.Count(p => p.IsActive)
            })
            .ToListAsync(cancellationToken);

        var featuredEntities = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Where(p => p.IsActive && p.IsFeatured)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Take(8)
            .ToListAsync(cancellationToken);

        var totalProducts = await _db.Products
            .AsNoTracking()
            .CountAsync(p => p.IsActive, cancellationToken);

        var categoryCards = categories.Select(item => new HomeCategoryCardViewModel
        {
            Id = item.Category.Id,
            Slug = item.Category.Slug,
            Name = item.Category.GetName(culture),
            Description = item.Category.GetDescription(culture),
            IconName = item.Category.IconName,
            ProductCount = item.Count
        }).ToList();

        var featuredCards = featuredEntities.Select(p =>
        {
            var primaryImage = p.Images
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.DisplayOrder)
                .FirstOrDefault()?.Url ?? string.Empty;

            return new HomeProductCardViewModel
            {
                Id = p.Id,
                Sku = p.Sku,
                Slug = p.Slug,
                Brand = p.Brand,
                CategoryName = p.Category?.GetName(culture) ?? string.Empty,
                Name = p.GetName(culture),
                ShortDescription = p.GetShortDescription(culture),
                Price = p.Price,
                DiscountPrice = p.DiscountPrice,
                EffectivePrice = p.EffectivePrice,
                HasDiscount = p.HasDiscount,
                DiscountPercent = p.DiscountPercent,
                StockQuantity = p.StockQuantity,
                ImageUrl = primaryImage
            };
        }).ToList();

        var viewModel = new HomeViewModel
        {
            TotalCategories = categoryCards.Count,
            TotalProducts = totalProducts,
            LowStockThreshold = _storeOptions.LowStockThreshold,
            HeroProduct = featuredCards.FirstOrDefault(),
            Categories = categoryCards,
            FeaturedProducts = featuredCards
        };

        return View(viewModel);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
