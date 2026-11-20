using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using TechStore.Models;

namespace TechStore.ViewModels;

// =========================================================================
// Dashboard
// =========================================================================

public sealed class AdminDashboardViewModel
{
    public decimal TotalRevenue { get; set; }
    public string FormattedTotalRevenue { get; set; } = string.Empty;
    public decimal RevenueLast30Days { get; set; }
    public string FormattedRevenueLast30Days { get; set; } = string.Empty;
    public int TotalOrders { get; set; }
    public int PaidOrdersCount { get; set; }
    public int TotalProducts { get; set; }
    public int ActiveProducts { get; set; }
    public int LowStockProductsCount { get; set; }
    public int OutOfStockProductsCount { get; set; }
    public int TotalCategories { get; set; }
    public int TotalCustomers { get; set; }
    public int LowStockThreshold { get; set; }

    public IReadOnlyList<AdminOrderStatusCountViewModel> OrderCountsByStatus { get; set; } = Array.Empty<AdminOrderStatusCountViewModel>();
    public IReadOnlyList<AdminLowStockProductItem> LowStockAlerts { get; set; } = Array.Empty<AdminLowStockProductItem>();
    public IReadOnlyList<AdminRecentOrderItem> RecentOrders { get; set; } = Array.Empty<AdminRecentOrderItem>();
}

public sealed class AdminOrderStatusCountViewModel
{
    public OrderStatus Status { get; set; }
    public int Count { get; set; }
    public string FormattedRevenue { get; set; } = string.Empty;
}

public sealed class AdminLowStockProductItem
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public string FormattedPrice { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class AdminRecentOrderItem
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public int ItemCount { get; set; }
    public string FormattedTotal { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

// =========================================================================
// Products
// =========================================================================

public sealed class AdminCategoryOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class AdminProductRowViewModel
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string FormattedPrice { get; set; } = string.Empty;
    public string? FormattedDiscountPrice { get; set; }
    public int StockQuantity { get; set; }
    public bool IsLowStock { get; set; }
    public bool IsOutOfStock { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; }
    public int ImageCount { get; set; }
    public int SpecCount { get; set; }
    public string PrimaryImageUrl { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class AdminProductListViewModel
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public string StockFilter { get; set; } = "all";
    public string StatusFilter { get; set; } = "all";
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
    public int LowStockThreshold { get; set; } = 5;
    public IReadOnlyList<AdminCategoryOption> Categories { get; set; } = Array.Empty<AdminCategoryOption>();
    public IReadOnlyList<AdminProductRowViewModel> Products { get; set; } = Array.Empty<AdminProductRowViewModel>();
}

public sealed class AdminProductSpecInputModel
{
    [MaxLength(120, ErrorMessage = "Validation.MaxLength")]
    public string KeyEn { get; set; } = string.Empty;

    [MaxLength(120, ErrorMessage = "Validation.MaxLength")]
    public string KeyRu { get; set; } = string.Empty;

    [MaxLength(120, ErrorMessage = "Validation.MaxLength")]
    public string KeyAz { get; set; } = string.Empty;

    [MaxLength(300, ErrorMessage = "Validation.MaxLength")]
    public string Value { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }
}

public sealed class AdminProductImageItem
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class AdminProductFormViewModel
{
    public int Id { get; set; }
    public bool IsEdit => Id > 0;

    [Required(ErrorMessage = "Validation.Required")]
    [MaxLength(60, ErrorMessage = "Validation.MaxLength")]
    public string Sku { get; set; } = string.Empty;

    [MaxLength(160, ErrorMessage = "Validation.MaxLength")]
    public string? Slug { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [MaxLength(80, ErrorMessage = "Validation.MaxLength")]
    public string Brand { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Validation.Required")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [MaxLength(200, ErrorMessage = "Validation.MaxLength")]
    public string NameEn { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [MaxLength(200, ErrorMessage = "Validation.MaxLength")]
    public string NameRu { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [MaxLength(200, ErrorMessage = "Validation.MaxLength")]
    public string NameAz { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [MaxLength(400, ErrorMessage = "Validation.MaxLength")]
    public string ShortDescriptionEn { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [MaxLength(400, ErrorMessage = "Validation.MaxLength")]
    public string ShortDescriptionRu { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [MaxLength(400, ErrorMessage = "Validation.MaxLength")]
    public string ShortDescriptionAz { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [MaxLength(4000, ErrorMessage = "Validation.MaxLength")]
    public string DescriptionEn { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [MaxLength(4000, ErrorMessage = "Validation.MaxLength")]
    public string DescriptionRu { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [MaxLength(4000, ErrorMessage = "Validation.MaxLength")]
    public string DescriptionAz { get; set; } = string.Empty;

    [Range(0.01, 9999999.99, ErrorMessage = "Admin.ValidationPrice")]
    public decimal Price { get; set; }

    [Range(0.01, 9999999.99, ErrorMessage = "Admin.ValidationDiscountPrice")]
    public decimal? DiscountPrice { get; set; }

    [Range(0, 1000000, ErrorMessage = "Admin.ValidationStock")]
    public int StockQuantity { get; set; }

    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; } = true;

    public List<AdminProductSpecInputModel> Specifications { get; set; } = new();
    public List<IFormFile>? NewImages { get; set; }
    public List<AdminProductImageItem> ExistingImages { get; set; } = new();
    public int? PrimaryImageId { get; set; }
    public List<int> RemoveImageIds { get; set; } = new();

    public IReadOnlyList<AdminCategoryOption> Categories { get; set; } = Array.Empty<AdminCategoryOption>();
}

// =========================================================================
// Categories
// =========================================================================

public sealed class AdminCategoryRowViewModel
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string LocalizedName { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameRu { get; set; } = string.Empty;
    public string NameAz { get; set; } = string.Empty;
    public string IconName { get; set; } = "cpu";
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public int ProductCount { get; set; }
}

public sealed class AdminCategoryListViewModel
{
    public IReadOnlyList<AdminCategoryRowViewModel> Categories { get; set; } = Array.Empty<AdminCategoryRowViewModel>();
}

public sealed class AdminCategoryFormViewModel
{
    public int Id { get; set; }
    public bool IsEdit => Id > 0;

    [MaxLength(100, ErrorMessage = "Validation.MaxLength")]
    public string? Slug { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [MaxLength(120, ErrorMessage = "Validation.MaxLength")]
    public string NameEn { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [MaxLength(120, ErrorMessage = "Validation.MaxLength")]
    public string NameRu { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [MaxLength(120, ErrorMessage = "Validation.MaxLength")]
    public string NameAz { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Validation.MaxLength")]
    public string? DescriptionEn { get; set; }

    [MaxLength(500, ErrorMessage = "Validation.MaxLength")]
    public string? DescriptionRu { get; set; }

    [MaxLength(500, ErrorMessage = "Validation.MaxLength")]
    public string? DescriptionAz { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [MaxLength(50, ErrorMessage = "Validation.MaxLength")]
    public string IconName { get; set; } = "cpu";

    [Range(0, 9999, ErrorMessage = "Admin.ValidationDisplayOrder")]
    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public int ProductCount { get; set; }
}

// =========================================================================
// Orders
// =========================================================================

public sealed class AdminOrderRowViewModel
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public int ItemCount { get; set; }
    public string FormattedTotal { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
}

public sealed class AdminOrderListViewModel
{
    public string? Search { get; set; }
    public OrderStatus? StatusFilter { get; set; }
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
    public int AllOrdersCount { get; set; }
    public Dictionary<OrderStatus, int> StatusCounts { get; set; } = new();
    public IReadOnlyList<AdminOrderRowViewModel> Orders { get; set; } = Array.Empty<AdminOrderRowViewModel>();
}

public sealed class AdminOrderDetailViewModel
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public OrderStatus Status { get; set; }
    public int StatusStep { get; set; }
    public bool IsCancelled { get; set; }
    public bool IsTerminal { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string ShippingCity { get; set; } = string.Empty;
    public string ShippingPostalCode { get; set; } = string.Empty;
    public string ShippingCountry { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Currency { get; set; } = "AZN";
    public string FormattedSubtotal { get; set; } = string.Empty;
    public string FormattedShipping { get; set; } = string.Empty;
    public string FormattedTotal { get; set; } = string.Empty;
    public bool IsFreeShipping { get; set; }
    public string? StripeCheckoutSessionId { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public IReadOnlyList<OrderItemDetailViewModel> Items { get; set; } = Array.Empty<OrderItemDetailViewModel>();
    public IReadOnlyList<OrderStatus> AllowedNextStatuses { get; set; } = Array.Empty<OrderStatus>();
}

// =========================================================================
// Customers
// =========================================================================

public sealed class AdminCustomerRowViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public bool IsAdmin { get; set; }
    public int OrdersCount { get; set; }
    public string FormattedTotalSpent { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class AdminCustomerListViewModel
{
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
    public IReadOnlyList<AdminCustomerRowViewModel> Customers { get; set; } = Array.Empty<AdminCustomerRowViewModel>();
}
