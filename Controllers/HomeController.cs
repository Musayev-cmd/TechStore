using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TechStore.Services;
using TechStore.Services.Localization;
using TechStore.ViewModels;

namespace TechStore.Controllers;

public class HomeController : Controller
{
    private readonly IProductCatalogService _catalogService;

    public HomeController(IProductCatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var culture = LocalizationHelper.CurrentCultureCode();
        var viewModel = await _catalogService.GetHomePageDataAsync(culture, cancellationToken);
        return View(viewModel);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
