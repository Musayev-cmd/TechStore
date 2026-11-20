using System.ComponentModel.DataAnnotations;

namespace TechStore.Models;

public class CartItem
{
    public int Id { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [MaxLength(64)]
    public string? GuestSessionId { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int Quantity { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
