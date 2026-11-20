using System.ComponentModel.DataAnnotations;

namespace TechStore.Models;

public class ProcessedStripeEvent
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string EventId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
}
