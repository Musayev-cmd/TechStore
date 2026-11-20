using System.ComponentModel.DataAnnotations;
using TechStore.Models;

namespace TechStore.ViewModels;

public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "Validation.Required")]
    [MaxLength(120, ErrorMessage = "Validation.MaxLength")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [EmailAddress(ErrorMessage = "Validation.Email")]
    [MaxLength(200, ErrorMessage = "Validation.MaxLength")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Validation.Phone")]
    [MaxLength(40, ErrorMessage = "Validation.MaxLength")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [MinLength(8, ErrorMessage = "Validation.MinLength")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [Compare(nameof(Password), ErrorMessage = "Validation.PasswordMismatch")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Validation.Required")]
    [EmailAddress(ErrorMessage = "Validation.Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class ProfileInputModel
{
    [Required(ErrorMessage = "Validation.Required")]
    [MaxLength(120, ErrorMessage = "Validation.MaxLength")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Validation.Phone")]
    [MaxLength(40, ErrorMessage = "Validation.MaxLength")]
    public string? PhoneNumber { get; set; }

    [MaxLength(10)]
    public string PreferredLanguage { get; set; } = "en";

    [MaxLength(250, ErrorMessage = "Validation.MaxLength")]
    public string? DefaultShippingAddress { get; set; }

    [MaxLength(100, ErrorMessage = "Validation.MaxLength")]
    public string? DefaultCity { get; set; }

    [MaxLength(30, ErrorMessage = "Validation.MaxLength")]
    public string? DefaultPostalCode { get; set; }

    [MaxLength(100, ErrorMessage = "Validation.MaxLength")]
    public string? DefaultCountry { get; set; }
}

public sealed class ChangePasswordInputModel
{
    [Required(ErrorMessage = "Validation.Required")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [MinLength(8, ErrorMessage = "Validation.MinLength")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [Compare(nameof(NewPassword), ErrorMessage = "Validation.PasswordMismatch")]
    [DataType(DataType.Password)]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public sealed class ProfilePageViewModel
{
    public string Email { get; set; } = string.Empty;
    public DateTime MemberSinceUtc { get; set; }
    public int TotalOrdersCount { get; set; }
    public int ActiveOrdersCount { get; set; }
    public ProfileInputModel Profile { get; set; } = new();
    public ChangePasswordInputModel Password { get; set; } = new();
}

public sealed class OrderHistoryViewModel
{
    public string? StatusFilter { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<OrderSummaryViewModel> Orders { get; set; } = Array.Empty<OrderSummaryViewModel>();
}

public sealed class OrderSummaryViewModel
{
    public string OrderNumber { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public int StatusStep { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public int ItemCount { get; set; }
    public string FormattedTotal { get; set; } = string.Empty;
    public IReadOnlyList<string> PreviewImages { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> PreviewNames { get; set; } = Array.Empty<string>();
}

public sealed class OrderItemDetailViewModel
{
    public int? ProductId { get; set; }
    public string? ProductSlug { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string FormattedUnitPrice { get; set; } = string.Empty;
    public string FormattedLineTotal { get; set; } = string.Empty;
}

public sealed class OrderDetailViewModel
{
    public string OrderNumber { get; set; } = string.Empty;
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
    public string FormattedSubtotal { get; set; } = string.Empty;
    public string FormattedShipping { get; set; } = string.Empty;
    public string FormattedTotal { get; set; } = string.Empty;
    public bool IsFreeShipping { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public IReadOnlyList<OrderItemDetailViewModel> Items { get; set; } = Array.Empty<OrderItemDetailViewModel>();
}
