using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.WebEncoders;
using TechStore.Data;
using TechStore.Models;
using TechStore.Options;
using TechStore.Services;
using TechStore.Services.Localization;

var builder = WebApplication.CreateBuilder(args);

// Bind strongly-typed configuration options (no secrets hardcoded in appsettings.json)
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<TechStore.Options.StoreOptions>(builder.Configuration.GetSection(TechStore.Options.StoreOptions.SectionName));
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(StripeOptions.SectionName));
builder.Services.Configure<SeedAdminOptions>(builder.Configuration.GetSection(SeedAdminOptions.SectionName));

// Database provider configuration: SQLite locally, one-setting switch to PostgreSQL in production
var dbOptions = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (string.Equals(dbOptions.Provider, "PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(dbOptions.Provider, "Postgres", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        var sqliteBuilder = new SqliteConnectionStringBuilder(connectionString);
        if (!string.IsNullOrWhiteSpace(sqliteBuilder.DataSource) &&
            !string.Equals(sqliteBuilder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            var fullPath = Path.IsPathRooted(sqliteBuilder.DataSource)
                ? sqliteBuilder.DataSource
                : Path.Combine(builder.Environment.ContentRootPath, sqliteBuilder.DataSource);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            sqliteBuilder.DataSource = fullPath;
        }

        options.UseSqlite(sqliteBuilder.ToString());
    }
});

// ASP.NET Core Identity with Admin & Customer roles
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "ts_auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
});

// Emit clean UTF-8 for English, Russian (Cyrillic), Azerbaijani (Latin Extended), and AZN symbol (₼) while preserving full HTML/XSS escaping
builder.Services.Configure<WebEncoderOptions>(options =>
{
    options.TextEncoderSettings = new TextEncoderSettings(
        UnicodeRanges.BasicLatin,
        UnicodeRanges.Latin1Supplement,
        UnicodeRanges.LatinExtendedA,
        UnicodeRanges.LatinExtendedB,
        UnicodeRanges.IpaExtensions,
        UnicodeRanges.Cyrillic,
        UnicodeRanges.GeneralPunctuation,
        UnicodeRanges.CurrencySymbols,
        UnicodeRanges.Arrows);
});

// Structured JSON localization (EN / RU / AZ) persisted via cookies
builder.Services.AddSingleton<JsonLocalizationStore>();
builder.Services.AddLocalization();
builder.Services.Replace(ServiceDescriptor.Singleton<IStringLocalizerFactory, JsonStringLocalizerFactory>());

var supportedCultures = JsonLocalizationStore.SupportedCultures
    .Select(code =>
    {
        var cultureInfo = new CultureInfo(code);
        cultureInfo.NumberFormat.NumberDecimalSeparator = ".";
        cultureInfo.NumberFormat.CurrencyDecimalSeparator = ".";
        return cultureInfo;
    })
    .ToList();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new CookieRequestCultureProvider(),
        new QueryStringRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    };
});

// Domain services
builder.Services.AddSingleton<ICurrencyFormatter, CurrencyFormatter>();
builder.Services.AddScoped<IProductCatalogService, ProductCatalogService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IStripeCheckoutService, StripeCheckoutService>();
builder.Services.AddScoped<IImageService, ImageService>();

// Security: CSRF protection for MVC & fetch API mutations
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
    options.Cookie.Name = "ts_csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
})
.AddDataAnnotationsLocalization(options =>
{
    options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource));
});

var app = builder.Build();

// Configure middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseRequestLocalization();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Run idempotent startup seeder when booted normally (skipped during dotnet ef design-time commands)
if (!EF.IsDesignTime)
{
    await DbSeeder.SeedAsync(app.Services);
}

app.Run();

public partial class Program
{
}
