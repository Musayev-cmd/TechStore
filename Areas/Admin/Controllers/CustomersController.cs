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
public sealed class CustomersController : AdminBaseController
{
    private const int PageSize = 15;

    private readonly AppDbContext _db;
    private readonly ICurrencyFormatter _currency;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public CustomersController(
        AppDbContext db,
        ICurrencyFormatter currency,
        IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _currency = currency;
        _localizer = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);

        var adminRoleId = await _db.Roles
            .AsNoTracking()
            .Where(r => r.Name == DbSeeder.AdminRole)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var adminUserIds = adminRoleId is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await _db.UserRoles
                .AsNoTracking()
                .Where(ur => ur.RoleId == adminRoleId)
                .Select(ur => ur.UserId)
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.Ordinal);

        var query = _db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(term) ||
                (u.Email != null && u.Email.ToLower().Contains(term)) ||
                (u.PhoneNumber != null && u.PhoneNumber.ToLower().Contains(term)) ||
                (u.DefaultCity != null && u.DefaultCity.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
        if (page > totalPages)
        {
            page = totalPages;
        }

        var users = await query
            .OrderByDescending(u => u.CreatedAtUtc)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        var userIds = users.Select(u => u.Id).ToList();

        var userOrders = userIds.Count == 0
            ? []
            : await _db.Orders
                .AsNoTracking()
                .Where(o => o.UserId != null && userIds.Contains(o.UserId))
                .Select(o => new
                {
                    UserId = o.UserId!,
                    o.Status,
                    o.TotalAmount
                })
                .ToListAsync(cancellationToken);

        var statsLookup = userOrders
            .GroupBy(o => o.UserId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (
                    OrdersCount: g.Count(),
                    TotalSpent: g
                        .Where(x => x.Status is OrderStatus.Paid or OrderStatus.Processing or OrderStatus.Shipped or OrderStatus.Delivered)
                        .Sum(x => x.TotalAmount)
                ),
                StringComparer.Ordinal);

        var rows = users.Select(u =>
        {
            var hasStats = statsLookup.TryGetValue(u.Id, out var stat);
            return new AdminCustomerRowViewModel
            {
                Id = u.Id,
                FullName = string.IsNullOrWhiteSpace(u.FullName) ? (u.Email ?? string.Empty) : u.FullName,
                Email = u.Email ?? string.Empty,
                PhoneNumber = u.PhoneNumber,
                City = u.DefaultCity,
                Country = u.DefaultCountry,
                PreferredLanguage = JsonLocalizationStore.NormalizeCulture(u.PreferredLanguage),
                IsAdmin = adminUserIds.Contains(u.Id),
                OrdersCount = hasStats ? stat.OrdersCount : 0,
                FormattedTotalSpent = _currency.Format(hasStats ? stat.TotalSpent : 0m),
                CreatedAtUtc = u.CreatedAtUtc
            };
        }).ToList();

        ViewData["Title"] = _localizer["Admin.CustomersTitle"].Value;

        return View(new AdminCustomerListViewModel
        {
            Search = search?.Trim(),
            Page = page,
            TotalPages = totalPages,
            TotalCount = totalCount,
            Customers = rows
        });
    }
}
