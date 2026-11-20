using System.Globalization;
using Microsoft.Extensions.Options;
using TechStore.Options;

namespace TechStore.Services;

public interface ICurrencyFormatter
{
    string CurrencyCode { get; }
    string CurrencySymbol { get; }
    string Format(decimal amount);
}

public sealed class CurrencyFormatter : ICurrencyFormatter
{
    private static readonly NumberFormatInfo PriceNumberFormat = new()
    {
        NumberGroupSeparator = " ",
        NumberDecimalSeparator = ".",
        NumberDecimalDigits = 2
    };

    private readonly StoreOptions _options;

    public CurrencyFormatter(IOptions<StoreOptions> options)
    {
        _options = options.Value;
    }

    public string CurrencyCode => _options.CurrencyCode;
    public string CurrencySymbol => _options.CurrencySymbol;

    public string Format(decimal amount) =>
        $"{amount.ToString("N2", PriceNumberFormat)} {_options.CurrencySymbol}";
}
