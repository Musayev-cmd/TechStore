using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Localization;

namespace TechStore.Services.Localization;

public sealed class JsonLocalizationStore
{
    public static readonly string[] SupportedCultures = ["en", "ru", "az"];

    private readonly string _resourcesPath;
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public JsonLocalizationStore(IWebHostEnvironment environment)
    {
        _resourcesPath = Path.Combine(environment.ContentRootPath, "Resources");
        if (!Directory.Exists(_resourcesPath))
        {
            _resourcesPath = Path.Combine(AppContext.BaseDirectory, "Resources");
        }
    }

    public IReadOnlyDictionary<string, string> GetDictionary(string culture)
    {
        var normalized = NormalizeCulture(culture);
        return _cache.GetOrAdd(normalized, LoadCultureFile);
    }

    public string? GetValue(string key, string culture)
    {
        var dict = GetDictionary(culture);
        if (dict.TryGetValue(key, out var value))
        {
            return value;
        }

        if (!string.Equals(culture, "en", StringComparison.OrdinalIgnoreCase))
        {
            var fallback = GetDictionary("en");
            if (fallback.TryGetValue(key, out var fallbackValue))
            {
                return fallbackValue;
            }
        }

        return null;
    }

    public IReadOnlyDictionary<string, string> GetClientDictionary(string culture)
    {
        var dict = GetDictionary(culture);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kvp in dict)
        {
            if (kvp.Key.StartsWith("Js.", StringComparison.Ordinal) ||
                kvp.Key.StartsWith("Stock.", StringComparison.Ordinal) ||
                kvp.Key.StartsWith("Cart.", StringComparison.Ordinal) ||
                kvp.Key.StartsWith("Catalog.", StringComparison.Ordinal) ||
                kvp.Key.StartsWith("Product.", StringComparison.Ordinal) ||
                kvp.Key.StartsWith("Theme.", StringComparison.Ordinal) ||
                kvp.Key.StartsWith("OrderStatus.", StringComparison.Ordinal) ||
                kvp.Key.StartsWith("Checkout.", StringComparison.Ordinal) ||
                kvp.Key.StartsWith("Orders.", StringComparison.Ordinal) ||
                kvp.Key.StartsWith("Common.", StringComparison.Ordinal))
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        return result;
    }

    public static string NormalizeCulture(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return "en";
        }

        var twoLetter = culture.Split('-', '_')[0].ToLowerInvariant();
        return SupportedCultures.Contains(twoLetter, StringComparer.OrdinalIgnoreCase)
            ? twoLetter
            : "en";
    }

    private IReadOnlyDictionary<string, string> LoadCultureFile(string culture)
    {
        var filePath = Path.Combine(_resourcesPath, $"{culture}.json");
        if (!File.Exists(filePath))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var json = File.ReadAllText(filePath);
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        return parsed ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }
}

public sealed class JsonStringLocalizer : IStringLocalizer
{
    private readonly JsonLocalizationStore _store;

    public JsonStringLocalizer(JsonLocalizationStore store)
    {
        _store = store;
    }

    public LocalizedString this[string name]
    {
        get
        {
            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var value = _store.GetValue(name, culture);
            return new LocalizedString(name, value ?? name, resourceNotFound: value is null);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var actual = this[name];
            var formatted = actual.ResourceNotFound
                ? actual.Value
                : string.Format(CultureInfo.CurrentCulture, actual.Value, arguments);
            return new LocalizedString(name, formatted, actual.ResourceNotFound);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        foreach (var kvp in _store.GetDictionary(culture))
        {
            yield return new LocalizedString(kvp.Key, kvp.Value, resourceNotFound: false);
        }
    }
}

public sealed class JsonStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly JsonLocalizationStore _store;

    public JsonStringLocalizerFactory(JsonLocalizationStore store)
    {
        _store = store;
    }

    public IStringLocalizer Create(Type resourceSource) => new JsonStringLocalizer(_store);

    public IStringLocalizer Create(string baseName, string location) => new JsonStringLocalizer(_store);
}
