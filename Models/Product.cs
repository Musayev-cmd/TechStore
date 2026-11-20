using System.ComponentModel.DataAnnotations;

namespace TechStore.Models;

public class Product
{
    public int Id { get; set; }

    [Required, MaxLength(60)]
    public string Sku { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string Slug { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string Brand { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    [Required, MaxLength(200)]
    public string NameEn { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string NameRu { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string NameAz { get; set; } = string.Empty;

    [Required, MaxLength(400)]
    public string ShortDescriptionEn { get; set; } = string.Empty;

    [Required, MaxLength(400)]
    public string ShortDescriptionRu { get; set; } = string.Empty;

    [Required, MaxLength(400)]
    public string ShortDescriptionAz { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string DescriptionEn { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string DescriptionRu { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string DescriptionAz { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public decimal? DiscountPrice { get; set; }

    public int StockQuantity { get; set; }

    public bool IsFeatured { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductSpecification> Specifications { get; set; } = new List<ProductSpecification>();

    public decimal EffectivePrice =>
        DiscountPrice.HasValue && DiscountPrice.Value > 0 && DiscountPrice.Value < Price
            ? DiscountPrice.Value
            : Price;

    public bool HasDiscount =>
        DiscountPrice.HasValue && DiscountPrice.Value > 0 && DiscountPrice.Value < Price;

    public int DiscountPercent =>
        HasDiscount && Price > 0
            ? (int)Math.Round((1m - (DiscountPrice!.Value / Price)) * 100m, MidpointRounding.AwayFromZero)
            : 0;
}
