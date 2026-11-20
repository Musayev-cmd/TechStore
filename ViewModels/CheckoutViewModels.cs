using System.ComponentModel.DataAnnotations;
using TechStore.Models;

namespace TechStore.ViewModels;

public sealed class CheckoutInputModel
{
    [Required(ErrorMessage = "Validation.Required")]
    [MaxLength(120, ErrorMessage = "Validation.MaxLength")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [EmailAddress(ErrorMessage = "Validation.Email")]
    [MaxLength(200, ErrorMessage = "Validation.MaxLength")]
    public string CustomerEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [Phone(ErrorMessage = "Validation.Phone")]
    [MaxLength(40, ErrorMessage = "Validation.MaxLength")]
    public string CustomerPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [MaxLength(300, ErrorMessage = "Validation.MaxLength")]
    public string ShippingAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [MaxLength(100, ErrorMessage = "Validation.MaxLength")]
    public string ShippingCity { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [MaxLength(30, ErrorMessage = "Validation.MaxLength")]
    public string ShippingPostalCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [MaxLength(100, ErrorMessage = "Validation.MaxLength")]
    public string ShippingCountry { get; set; } = "Azerbaijan";

    [MaxLength(500, ErrorMessage = "Validation.MaxLength")]
    public string? Notes { get; set; }

    public bool SaveAddressToProfile { get; set; }
}

public sealed class CheckoutViewModel
{
    public CheckoutInputModel Input { get; set; } = new();
    public CartViewModel Cart { get; set; } = new();
    public bool IsAuthenticated { get; set; }
    public bool StripeConfigured { get; set; }
}

public sealed class CheckoutSuccessViewModel
{
    public string OrderNumber { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public OrderStatus Status { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string ShippingCity { get; set; } = string.Empty;
    public string ShippingPostalCode { get; set; } = string.Empty;
    public string ShippingCountry { get; set; } = string.Empty;
    public string FormattedSubtotal { get; set; } = string.Empty;
    public string FormattedShipping { get; set; } = string.Empty;
    public string FormattedTotal { get; set; } = string.Empty;
    public bool IsFreeShipping { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public bool IsAuthenticated { get; set; }
    public IReadOnlyList<OrderItemDetailViewModel> Items { get; set; } = Array.Empty<OrderItemDetailViewModel>();
}

public sealed class CheckoutCancelViewModel
{
    public string? OrderNumber { get; set; }
    public int CartItemCount { get; set; }
}

public sealed class OrderStatusPollViewModel
{
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int StatusStep { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public bool IsPaid { get; set; }
    public bool IsCancelled { get; set; }
    public bool IsTerminal { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
}
