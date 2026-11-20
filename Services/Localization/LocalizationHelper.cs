using System.Globalization;
using TechStore.Models;

namespace TechStore.Services.Localization;

public static class LocalizationHelper
{
    public static string CurrentCultureCode() =>
        JsonLocalizationStore.NormalizeCulture(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public static string GetName(this Category category, string? culture = null) =>
        JsonLocalizationStore.NormalizeCulture(culture ?? CurrentCultureCode()) switch
        {
            "ru" => string.IsNullOrWhiteSpace(category.NameRu) ? category.NameEn : category.NameRu,
            "az" => string.IsNullOrWhiteSpace(category.NameAz) ? category.NameEn : category.NameAz,
            _ => category.NameEn
        };

    public static string GetDescription(this Category category, string? culture = null) =>
        JsonLocalizationStore.NormalizeCulture(culture ?? CurrentCultureCode()) switch
        {
            "ru" => category.DescriptionRu ?? category.DescriptionEn ?? string.Empty,
            "az" => category.DescriptionAz ?? category.DescriptionEn ?? string.Empty,
            _ => category.DescriptionEn ?? string.Empty
        };

    public static string GetName(this Product product, string? culture = null) =>
        JsonLocalizationStore.NormalizeCulture(culture ?? CurrentCultureCode()) switch
        {
            "ru" => string.IsNullOrWhiteSpace(product.NameRu) ? product.NameEn : product.NameRu,
            "az" => string.IsNullOrWhiteSpace(product.NameAz) ? product.NameEn : product.NameAz,
            _ => product.NameEn
        };

    public static string GetShortDescription(this Product product, string? culture = null) =>
        JsonLocalizationStore.NormalizeCulture(culture ?? CurrentCultureCode()) switch
        {
            "ru" => string.IsNullOrWhiteSpace(product.ShortDescriptionRu) ? product.ShortDescriptionEn : product.ShortDescriptionRu,
            "az" => string.IsNullOrWhiteSpace(product.ShortDescriptionAz) ? product.ShortDescriptionEn : product.ShortDescriptionAz,
            _ => product.ShortDescriptionEn
        };

    public static string GetDescription(this Product product, string? culture = null) =>
        JsonLocalizationStore.NormalizeCulture(culture ?? CurrentCultureCode()) switch
        {
            "ru" => string.IsNullOrWhiteSpace(product.DescriptionRu) ? product.DescriptionEn : product.DescriptionRu,
            "az" => string.IsNullOrWhiteSpace(product.DescriptionAz) ? product.DescriptionEn : product.DescriptionAz,
            _ => product.DescriptionEn
        };

    public static string GetKey(this ProductSpecification spec, string? culture = null) =>
        JsonLocalizationStore.NormalizeCulture(culture ?? CurrentCultureCode()) switch
        {
            "ru" => string.IsNullOrWhiteSpace(spec.KeyRu) ? spec.KeyEn : spec.KeyRu,
            "az" => string.IsNullOrWhiteSpace(spec.KeyAz) ? spec.KeyEn : spec.KeyAz,
            _ => spec.KeyEn
        };

    public static string GetStatusResourceKey(this OrderStatus status) => $"OrderStatus.{status}";
}
