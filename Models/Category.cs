using System.ComponentModel.DataAnnotations;

namespace TechStore.Models;

public class Category
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Slug { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string NameEn { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string NameRu { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string NameAz { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? DescriptionEn { get; set; }

    [MaxLength(500)]
    public string? DescriptionRu { get; set; }

    [MaxLength(500)]
    public string? DescriptionAz { get; set; }

    [MaxLength(50)]
    public string IconName { get; set; } = "cpu";

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
