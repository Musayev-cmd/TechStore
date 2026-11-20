namespace TechStore.Options;

public sealed class StoreOptions
{
    public const string SectionName = "Store";

    public string Name { get; set; } = "TechStore";
    public string CurrencyCode { get; set; } = "AZN";
    public string CurrencySymbol { get; set; } = "₼";
    public decimal FreeShippingThreshold { get; set; } = 250.00m;
    public decimal FlatShippingFee { get; set; } = 10.00m;
    public int LowStockThreshold { get; set; } = 5;
}
