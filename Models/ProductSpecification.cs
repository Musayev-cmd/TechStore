using System.ComponentModel.DataAnnotations;

namespace TechStore.Models;

public class ProductSpecification
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Required, MaxLength(120)]
    public string KeyEn { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string KeyRu { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string KeyAz { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Value { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }
}
