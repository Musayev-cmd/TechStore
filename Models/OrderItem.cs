using System.ComponentModel.DataAnnotations;

namespace TechStore.Models;

public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int? ProductId { get; set; }
    public Product? Product { get; set; }

    [Required, MaxLength(200)]
    public string ProductNameSnapshot { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string ProductSkuSnapshot { get; set; } = string.Empty;

    [MaxLength(400)]
    public string? ProductImageSnapshot { get; set; }

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal LineTotal { get; set; }
}
