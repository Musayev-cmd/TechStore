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
public sealed class DashboardController : AdminBaseController
{
    private readonly AppDbContext _db;
    private readonly ICurrencyFormatter _currency;
    private readonly StoreOptions _storeOptions;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public DashboardController(
        AppDbContext db,
        ICurrencyFormatter currency,
        IOptions<StoreOptions> storeOptions,
        IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _currency = currency;
        _storeOptions = storeOptions.Value;
        _localizer = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var culture = LocalizationHelper.CurrentCultureCode();
        var lowStockThreshold = _storeOptions.LowStockThreshold;
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

        var statusRows = await _db.Orders
            .AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new
            {
                Status = g.Key,
                Count = g.Count(),
                Revenue = g.Sum(x => x.TotalAmount)
            })
            .ToListAsync(cancellationToken);

        var statusLookup = statusRows.ToDictionary(x => x.Status, x => (x.Count, x.Revenue));

        var orderCountsByStatus = Enum.GetValues<OrderStatus>()
            .Select(status =>
            {
                statusLookup.TryGetValue(status, out var stat);
                return new AdminOrderStatusCountViewModel
                {
                    Status = status,
                    Count = stat.Count,
                    FormattedRevenue = _currency.Format(stat.Revenue)
                };
            })
            .ToList();

        var totalOrders = statusRows.Sum(x => x.Count);
        var totalRevenue = statusRows
            .Where(x => x.Status is OrderStatus.Paid or OrderStatus.Processing or OrderStatus.Shipped or OrderStatus.Delivered)
            .Sum(x => x.Revenue);
        var paidOrdersCount = statusRows
            .Where(x => x.Status is OrderStatus.Paid or OrderStatus.Processing or OrderStatus.Shipped or OrderStatus.Delivered)
            .Sum(x => x.Count);

        var revenueLast30Days = await _db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAtUtc >= thirtyDaysAgo &&
                        (o.Status == OrderStatus.Paid ||
                         o.Status == OrderStatus.Processing ||
                         o.Status == OrderStatus.Shipped ||
                         o.Status == OrderStatus.Delivered))
            .Select(o => (decimal?)o.TotalAmount)
            .SumAsync(cancellationToken) ?? 0m;

        var totalProducts = await _db.Products.AsNoTracking().CountAsync(cancellationToken);
        var activeProducts = await _db.Products.AsNoTracking().CountAsync(p => p.IsActive, cancellationToken);
        var outOfStockProducts = await _db.Products.AsNoTracking().CountAsync(p => p.IsActive && p.StockQuantity <= 0, cancellationToken);
        var lowStockProductsCount = await _db.Products.AsNoTracking()
            .CountAsync(p => p.IsActive && p.StockQuantity > 0 && p.StockQuantity <= lowStockThreshold, cancellationToken);

        var totalCategories = await _db.Categories.AsNoTracking().CountAsync(cancellationToken);
        var totalCustomers = await _db.Users.AsNoTracking().CountAsync(cancellationToken);

        var lowStockEntities = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Where(p => p.IsActive && p.StockQuantity <= lowStockThreshold)
            .OrderBy(p => p.StockQuantity)
            .ThenBy(p => p.NameEn)
            .Take(10)
            .ToListAsync(cancellationToken);

        var lowStockAlerts = lowStockEntities
            .Select(p =>
            {
                var primaryImg = p.Images
                    .OrderByDescending(i => i.IsPrimary)
                    .ThenBy(i => i.DisplayOrder)
                    .FirstOrDefault()?.Url ?? string.Empty;

                return new AdminLowStockProductItem
                {
                    Id = p.Id,
                    Sku = p.Sku,
                    Name = p.GetName(culture),
                    Brand = p.Brand,
                    CategoryName = p.Category?.GetName(culture) ?? string.Empty,
                    StockQuantity = p.StockQuantity,
                    FormattedPrice = _currency.Format(p.EffectivePrice),
                    ImageUrl = primaryImg,
                    IsActive = p.IsActive
                };
            })
            .ToList();

        var recentOrderEntities = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);

        var recentOrders = recentOrderEntities
            .Select(o => new AdminRecentOrderItem
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                CustomerName = o.CustomerName,
                CustomerEmail = o.CustomerEmail,
                Status = o.Status,
                ItemCount = o.Items.Sum(i => i.Quantity),
                FormattedTotal = _currency.Format(o.TotalAmount),
                CreatedAtUtc = o.CreatedAtUtc
            })
            .ToList();

        ViewData["Title"] = _localizer["Admin.DashboardTitle"].Value;

        return View(new AdminDashboardViewModel
        {
            TotalRevenue = totalRevenue,
            FormattedTotalRevenue = _currency.Format(totalRevenue),
            RevenueLast30Days = revenueLast30Days,
            FormattedRevenueLast30Days = _currency.Format(revenueLast30Days),
            TotalOrders = totalOrders,
            PaidOrdersCount = paidOrdersCount,
            TotalProducts = totalProducts,
            ActiveProducts = activeProducts,
            LowStockProductsCount = lowStockProductsCount,
            OutOfStockProductsCount = outOfStockProducts,
            TotalCategories = totalCategories,
            TotalCustomers = totalCustomers,
            LowStockThreshold = lowStockThreshold,
            OrderCountsByStatus = orderCountsByStatus,
            LowStockAlerts = lowStockAlerts,
            RecentOrders = recentOrders
        });
    }
}
