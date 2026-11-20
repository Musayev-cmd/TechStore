namespace TechStore.Options;

public sealed class SeedAdminOptions
{
    public const string SectionName = "SeedAdmin";

    public string Email { get; set; } = "admin@techstore.local";
    public string FullName { get; set; } = "TechStore Administrator";
    public string Password { get; set; } = string.Empty;
}
