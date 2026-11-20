using System.ComponentModel.DataAnnotations;

namespace TechStore.Models;

public class Order
{
    public int Id { get; set; }

    [Required, MaxLength(32)]
    public string OrderNumber { get; set; } = string.Empty;

    [MaxLength(450)]
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [MaxLength(64)]
    public string? GuestSessionId { get; set; }

    [Required, MaxLength(200)]
    public string CustomerEmail { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string CustomerPhone { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string ShippingAddress { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ShippingCity { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string ShippingPostalCode { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ShippingCountry { get; set; } = "Azerbaijan";

    [MaxLength(500)]
    public string? Notes { get; set; }

    [Required, MaxLength(10)]
    public string Currency { get; set; } = "AZN";

    public decimal SubtotalAmount { get; set; }

    public decimal ShippingAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    [MaxLength(200)]
    public string? StripeCheckoutSessionId { get; set; }

    [MaxLength(200)]
    public string? StripePaymentIntentId { get; set; }

    public DateTime? PaidAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
