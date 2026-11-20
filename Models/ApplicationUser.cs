using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace TechStore.Models;

public class ApplicationUser : IdentityUser
{
    [MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(10)]
    public string PreferredLanguage { get; set; } = "en";

    [MaxLength(250)]
    public string? DefaultShippingAddress { get; set; }

    [MaxLength(100)]
    public string? DefaultCity { get; set; }

    [MaxLength(30)]
    public string? DefaultPostalCode { get; set; }

    [MaxLength(100)]
    public string? DefaultCountry { get; set; } = "Azerbaijan";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
}
