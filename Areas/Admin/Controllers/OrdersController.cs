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
public sealed class OrdersController : AdminBaseController
{
    private const int PageSize = 15;

    private readonly AppDbContext _db;
    private readonly ICurrencyFormatter _currency;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        AppDbContext db,
        ICurrencyFormatter currency,
        IStringLocalizer<SharedResource> localizer,
        ILogger<OrdersController> logger)
    {
        _db = db;
        _currency = currency;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        OrderStatus? status,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);

        var statusCountsRaw = await _db.Orders
            .AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var statusCounts = Enum.GetValues<OrderStatus>()
            .ToDictionary(s => s, s => statusCountsRaw.FirstOrDefault(r => r.Status == s)?.Count ?? 0);

        var allOrdersCount = statusCounts.Values.Sum();

        var query = _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .AsQueryable();

        if (status.HasValue && Enum.IsDefined(status.Value))
        {
            query = query.Where(o => o.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(o =>
                o.OrderNumber.ToLower().Contains(term) ||
                o.CustomerName.ToLower().Contains(term) ||
                o.CustomerEmail.ToLower().Contains(term) ||
                o.CustomerPhone.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
        if (page > totalPages)
        {
            page = totalPages;
        }

        var entities = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        var rows = entities.Select(o => new AdminOrderRowViewModel
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            CustomerName = o.CustomerName,
            CustomerEmail = o.CustomerEmail,
            Status = o.Status,
            ItemCount = o.Items.Sum(i => i.Quantity),
            FormattedTotal = _currency.Format(o.TotalAmount),
            CreatedAtUtc = o.CreatedAtUtc,
            PaidAtUtc = o.PaidAtUtc
        }).ToList();

        ViewData["Title"] = _localizer["Admin.OrdersTitle"].Value;

        return View(new AdminOrderListViewModel
        {
            Search = search?.Trim(),
            StatusFilter = status,
            Page = page,
            TotalPages = totalPages,
            TotalCount = totalCount,
            AllOrdersCount = allOrdersCount,
            StatusCounts = statusCounts,
            Orders = rows
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        ViewData["Title"] = $"{_localizer["Orders.DetailsTitle"].Value} {order.OrderNumber}";

        var items = order.Items.Select(i => new OrderItemDetailViewModel
        {
            ProductId = i.ProductId,
            ProductSlug = i.Product?.Slug,
            ProductName = i.ProductNameSnapshot,
            ProductSku = i.ProductSkuSnapshot,
            ImageUrl = i.ProductImageSnapshot ?? string.Empty,
            Quantity = i.Quantity,
            FormattedUnitPrice = _currency.Format(i.UnitPrice),
            FormattedLineTotal = _currency.Format(i.LineTotal)
        }).ToList();

        var allowedNext = AdminHelpers.GetAllowedNextStatuses(order.Status);

        return View(new AdminOrderDetailViewModel
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            UserId = order.UserId,
            Status = order.Status,
            StatusStep = AdminHelpers.GetOrderStatusStep(order.Status),
            IsCancelled = order.Status == OrderStatus.Cancelled,
            IsTerminal = allowedNext.Count == 0,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            CustomerPhone = order.CustomerPhone,
            ShippingAddress = order.ShippingAddress,
            ShippingCity = order.ShippingCity,
            ShippingPostalCode = order.ShippingPostalCode,
            ShippingCountry = order.ShippingCountry,
            Notes = order.Notes,
            Currency = order.Currency,
            FormattedSubtotal = _currency.Format(order.SubtotalAmount),
            FormattedShipping = order.ShippingAmount <= 0m
                ? _localizer["Cart.FreeShipping"].Value
                : _currency.Format(order.ShippingAmount),
            FormattedTotal = _currency.Format(order.TotalAmount),
            IsFreeShipping = order.ShippingAmount <= 0m,
            StripeCheckoutSessionId = order.StripeCheckoutSessionId,
            StripePaymentIntentId = order.StripePaymentIntentId,
            PaidAtUtc = order.PaidAtUtc,
            CreatedAtUtc = order.CreatedAtUtc,
            UpdatedAtUtc = order.UpdatedAtUtc,
            Items = items,
            AllowedNextStatuses = allowedNext
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus status, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(status))
        {
            SetErrorNotice(_localizer["Admin.OrderStatusInvalid"].Value);
            return RedirectToAction(nameof(Details), new { id });
        }

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        if (order.Status == status)
        {
            SetWarningNotice(_localizer["Admin.OrderStatusUnchanged"].Value);
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!AdminHelpers.CanTransitionOrderStatus(order.Status, status))
        {
            SetErrorNotice(_localizer[
                "Admin.OrderStatusTransitionDenied",
                _localizer[order.Status.GetStatusResourceKey()].Value,
                _localizer[status.GetStatusResourceKey()].Value].Value);
            return RedirectToAction(nameof(Details), new { id });
        }

        var previousStatus = order.Status;
        order.Status = status;
        if (status == OrderStatus.Paid && !order.PaidAtUtc.HasValue)
        {
            order.PaidAtUtc = DateTime.UtcNow;
        }
        order.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Admin {Admin} updated order {OrderNumber} status from {OldStatus} to {NewStatus}.",
            User.Identity?.Name,
            order.OrderNumber,
            previousStatus,
            status);

        SetSuccessNotice(_localizer[
            "Admin.OrderStatusUpdatedSuccess",
            order.OrderNumber,
            _localizer[status.GetStatusResourceKey()].Value].Value);

        return RedirectToAction(nameof(Details), new { id });
    }
}
