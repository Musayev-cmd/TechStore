using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TechStore.Models;

namespace TechStore.Services;

public static partial class AdminHelpers
{
    public static readonly IReadOnlyList<string> CategoryIcons =
    [
        "laptop",
        "cpu",
        "monitor",
        "keyboard",
        "drive",
        "dock"
    ];

    public static readonly IReadOnlyList<OrderStatus> RevenueOrderStatuses =
    [
        OrderStatus.Paid,
        OrderStatus.Processing,
        OrderStatus.Shipped,
        OrderStatus.Delivered
    ];

    public static string GenerateSlug(string? rawInput, int maxLength = 150)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return string.Empty;
        }

        var normalized = rawInput.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (ch is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
            else if (ch is ' ' or '-' or '_' or '/' or '.')
            {
                sb.Append('-');
            }
        }

        var collapsed = MultiDashRegex().Replace(sb.ToString(), "-").Trim('-');
        if (collapsed.Length > maxLength)
        {
            collapsed = collapsed[..maxLength].TrimEnd('-');
        }

        return collapsed;
    }

    public static bool IsValidCategoryIcon(string? iconName) =>
        !string.IsNullOrWhiteSpace(iconName) &&
        CategoryIcons.Contains(iconName.Trim().ToLowerInvariant(), StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<OrderStatus> GetAllowedNextStatuses(OrderStatus current) =>
        current switch
        {
            OrderStatus.Pending => [OrderStatus.Paid, OrderStatus.Processing, OrderStatus.Cancelled],
            OrderStatus.Paid => [OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Delivered, OrderStatus.Cancelled],
            OrderStatus.Processing => [OrderStatus.Shipped, OrderStatus.Delivered, OrderStatus.Cancelled],
            OrderStatus.Shipped => [OrderStatus.Delivered, OrderStatus.Cancelled],
            OrderStatus.Delivered => Array.Empty<OrderStatus>(),
            OrderStatus.Cancelled => Array.Empty<OrderStatus>(),
            _ => Array.Empty<OrderStatus>()
        };

    public static bool CanTransitionOrderStatus(OrderStatus current, OrderStatus next) =>
        current == next || GetAllowedNextStatuses(current).Contains(next);

    public static string GetOrderBadgeClass(OrderStatus status) =>
        status switch
        {
            OrderStatus.Paid or OrderStatus.Delivered => "badge-success",
            OrderStatus.Processing or OrderStatus.Shipped => "badge-accent",
            OrderStatus.Cancelled => "badge-danger",
            _ => "badge-warning"
        };

    public static int GetOrderStatusStep(OrderStatus status) =>
        status switch
        {
            OrderStatus.Pending => 0,
            OrderStatus.Paid => 1,
            OrderStatus.Processing => 2,
            OrderStatus.Shipped => 3,
            OrderStatus.Delivered => 4,
            _ => -1
        };

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultiDashRegex();
}
