using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using TechStore.Data;
using TechStore.Models;
using TechStore.Services;
using TechStore.Services.Localization;
using TechStore.ViewModels;

namespace TechStore.Controllers;

public sealed class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AppDbContext _db;
    private readonly ICartService _cartService;
    private readonly ICurrencyFormatter _currency;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        AppDbContext db,
        ICartService cartService,
        ICurrencyFormatter currency,
        IStringLocalizer<SharedResource> localizer,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _cartService = cartService;
        _currency = currency;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpGet("/Account/Register")]
    public IActionResult Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Profile));
        }

        ViewData["Title"] = _localizer["Account.RegisterTitle"].Value;
        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("/Account/Register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = _localizer["Account.RegisterTitle"].Value;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim();
        var currentCulture = LocalizationHelper.CurrentCultureCode();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = model.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
            PreferredLanguage = currentCulture,
            DefaultCountry = "Azerbaijan",
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            AddIdentityErrors(createResult);
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, DbSeeder.CustomerRole);
        await _signInManager.SignInAsync(user, isPersistent: false);

        await MergeGuestCartIfPresentAsync(user.Id, cancellationToken);

        _logger.LogInformation("New customer registered: {UserId}.", user.Id);

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return RedirectToAction(nameof(Profile));
    }

    [HttpGet("/Account/Login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Profile));
        }

        ViewData["Title"] = _localizer["Account.LoginTitle"].Value;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("/Account/Login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = _localizer["Account.LoginTitle"].Value;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email);
        var userName = user?.UserName ?? email;

        var signInResult = await _signInManager.PasswordSignInAsync(
            userName,
            model.Password,
            isPersistent: model.RememberMe,
            lockoutOnFailure: true);

        if (signInResult.Succeeded)
        {
            if (user is not null)
            {
                await MergeGuestCartIfPresentAsync(user.Id, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return LocalRedirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        if (signInResult.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, _localizer["Account.LockoutError"].Value);
            return View(model);
        }

        ModelState.AddModelError(string.Empty, _localizer["Account.InvalidCredentials"].Value);
        return View(model);
    }

    [HttpPost("/Account/Logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpGet("/Account/Profile")]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        ViewData["Title"] = _localizer["Account.ProfileTitle"].Value;
        var viewModel = await BuildProfilePageViewModelAsync(user, profileOverride: null, passwordOverride: null, cancellationToken);
        return View(viewModel);
    }

    [Authorize]
    [HttpPost("/Account/Profile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(
        [Bind(Prefix = "Profile")] ProfileInputModel input,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        ViewData["Title"] = _localizer["Account.ProfileTitle"].Value;

        if (!ModelState.IsValid)
        {
            var invalidVm = await BuildProfilePageViewModelAsync(user, input, passwordOverride: null, cancellationToken);
            return View(invalidVm);
        }

        var normalizedLang = JsonLocalizationStore.NormalizeCulture(input.PreferredLanguage);

        user.FullName = input.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim();
        user.PreferredLanguage = normalizedLang;
        user.DefaultShippingAddress = string.IsNullOrWhiteSpace(input.DefaultShippingAddress) ? null : input.DefaultShippingAddress.Trim();
        user.DefaultCity = string.IsNullOrWhiteSpace(input.DefaultCity) ? null : input.DefaultCity.Trim();
        user.DefaultPostalCode = string.IsNullOrWhiteSpace(input.DefaultPostalCode) ? null : input.DefaultPostalCode.Trim();
        user.DefaultCountry = string.IsNullOrWhiteSpace(input.DefaultCountry) ? "Azerbaijan" : input.DefaultCountry.Trim();

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            AddIdentityErrors(updateResult);
            var errorVm = await BuildProfilePageViewModelAsync(user, input, passwordOverride: null, cancellationToken);
            return View(errorVm);
        }

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(normalizedLang)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });

        TempData["AccountSuccess"] = _localizer["Account.ProfileUpdated"].Value;
        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    [HttpPost("/Account/ChangePassword")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(
        [Bind(Prefix = "Password")] ChangePasswordInputModel input,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        ViewData["Title"] = _localizer["Account.ProfileTitle"].Value;

        if (!ModelState.IsValid)
        {
            var invalidVm = await BuildProfilePageViewModelAsync(user, profileOverride: null, input, cancellationToken);
            return View("Profile", invalidVm);
        }

        var result = await _userManager.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword);
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            var errorVm = await BuildProfilePageViewModelAsync(user, profileOverride: null, input, cancellationToken);
            return View("Profile", errorVm);
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["AccountSuccess"] = _localizer["Account.PasswordUpdated"].Value;
        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    [HttpGet("/Account/Orders")]
    public async Task<IActionResult> Orders(
        [FromQuery(Name = "status")] string? status,
        CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var query = _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);
        string? activeFilter = null;

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<OrderStatus>(status.Trim(), ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(o => o.Status == parsedStatus);
            activeFilter = parsedStatus.ToString();
        }

        var orders = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var viewModel = new OrderHistoryViewModel
        {
            StatusFilter = activeFilter,
            TotalCount = totalCount,
            Orders = orders.Select(o => new OrderSummaryViewModel
            {
                OrderNumber = o.OrderNumber,
                Status = o.Status,
                StatusStep = CheckoutController.GetStatusStep(o.Status),
                CreatedAtUtc = o.CreatedAtUtc,
                PaidAtUtc = o.PaidAtUtc,
                ItemCount = o.Items.Sum(i => i.Quantity),
                FormattedTotal = _currency.Format(o.TotalAmount),
                PreviewImages = o.Items
                    .Select(i => i.ProductImageSnapshot)
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .Cast<string>()
                    .Take(3)
                    .ToList(),
                PreviewNames = o.Items
                    .Select(i => i.ProductNameSnapshot)
                    .Take(2)
                    .ToList()
            }).ToList()
        };

        ViewData["Title"] = _localizer["Orders.Title"].Value;
        return View(viewModel);
    }

    [Authorize]
    [HttpGet("/Account/Orders/{orderNumber}")]
    public async Task<IActionResult> OrderDetails(string orderNumber, CancellationToken cancellationToken)
    {
        var order = await FindUserOrderAsync(orderNumber, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        var viewModel = new OrderDetailViewModel
        {
            OrderNumber = order.OrderNumber,
            Status = order.Status,
            StatusStep = CheckoutController.GetStatusStep(order.Status),
            IsCancelled = order.Status == OrderStatus.Cancelled,
            IsTerminal = order.Status is OrderStatus.Delivered or OrderStatus.Cancelled,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            CustomerPhone = order.CustomerPhone,
            ShippingAddress = order.ShippingAddress,
            ShippingCity = order.ShippingCity,
            ShippingPostalCode = order.ShippingPostalCode,
            ShippingCountry = order.ShippingCountry,
            Notes = order.Notes,
            FormattedSubtotal = _currency.Format(order.SubtotalAmount),
            FormattedShipping = order.ShippingAmount == 0m
                ? _localizer["Cart.FreeShipping"].Value
                : _currency.Format(order.ShippingAmount),
            FormattedTotal = _currency.Format(order.TotalAmount),
            IsFreeShipping = order.ShippingAmount == 0m,
            CreatedAtUtc = order.CreatedAtUtc,
            PaidAtUtc = order.PaidAtUtc,
            UpdatedAtUtc = order.UpdatedAtUtc,
            Items = order.Items.Select(i => new OrderItemDetailViewModel
            {
                ProductId = i.ProductId,
                ProductSlug = i.Product?.Slug,
                ProductName = i.ProductNameSnapshot,
                ProductSku = i.ProductSkuSnapshot,
                ImageUrl = i.ProductImageSnapshot ?? string.Empty,
                Quantity = i.Quantity,
                FormattedUnitPrice = _currency.Format(i.UnitPrice),
                FormattedLineTotal = _currency.Format(i.LineTotal)
            }).ToList()
        };

        ViewData["Title"] = $"{_localizer["Orders.DetailsTitle"].Value} {order.OrderNumber}";
        return View(viewModel);
    }

    [Authorize]
    [HttpGet("/Account/OrderStatus/{orderNumber}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> OrderStatusApi(string orderNumber, CancellationToken cancellationToken)
    {
        var order = await FindUserOrderAsync(orderNumber, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        return Ok(new OrderStatusPollViewModel
        {
            OrderNumber = order.OrderNumber,
            Status = order.Status.ToString(),
            StatusStep = CheckoutController.GetStatusStep(order.Status),
            StatusLabel = _localizer[order.Status.GetStatusResourceKey()].Value,
            IsPaid = order.Status is OrderStatus.Paid or OrderStatus.Processing or OrderStatus.Shipped or OrderStatus.Delivered,
            IsCancelled = order.Status == OrderStatus.Cancelled,
            IsTerminal = order.Status is OrderStatus.Delivered or OrderStatus.Cancelled,
            UpdatedAtUtc = order.UpdatedAtUtc,
            PaidAtUtc = order.PaidAtUtc
        });
    }

    [HttpGet("/Account/AccessDenied")]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        ViewData["Title"] = _localizer["Account.AccessDeniedTitle"].Value;
        return View();
    }

    private async Task<Order?> FindUserOrderAsync(string orderNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return null;
        }

        var trimmed = orderNumber.Trim();
        var userId = _userManager.GetUserId(User);
        var isAdmin = User.IsInRole(DbSeeder.AdminRole);

        var query = _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Where(o => o.OrderNumber == trimmed);

        if (!isAdmin)
        {
            query = query.Where(o => o.UserId == userId);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private async Task MergeGuestCartIfPresentAsync(string userId, CancellationToken cancellationToken)
    {
        var guestSessionId = CartSessionCookieHelper.GetExistingGuestSessionId(Request);
        if (!string.IsNullOrWhiteSpace(guestSessionId))
        {
            await _cartService.MergeGuestCartAsync(guestSessionId, userId, cancellationToken);
            CartSessionCookieHelper.ClearGuestSessionCookie(HttpContext);
        }
    }

    private async Task<ProfilePageViewModel> BuildProfilePageViewModelAsync(
        ApplicationUser user,
        ProfileInputModel? profileOverride,
        ChangePasswordInputModel? passwordOverride,
        CancellationToken cancellationToken)
    {
        var totalOrders = await _db.Orders
            .AsNoTracking()
            .CountAsync(o => o.UserId == user.Id, cancellationToken);

        var activeOrders = await _db.Orders
            .AsNoTracking()
            .CountAsync(
                o => o.UserId == user.Id &&
                     o.Status != OrderStatus.Delivered &&
                     o.Status != OrderStatus.Cancelled,
                cancellationToken);

        return new ProfilePageViewModel
        {
            Email = user.Email ?? string.Empty,
            MemberSinceUtc = user.CreatedAtUtc,
            TotalOrdersCount = totalOrders,
            ActiveOrdersCount = activeOrders,
            Profile = profileOverride ?? new ProfileInputModel
            {
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                PreferredLanguage = JsonLocalizationStore.NormalizeCulture(user.PreferredLanguage),
                DefaultShippingAddress = user.DefaultShippingAddress,
                DefaultCity = user.DefaultCity,
                DefaultPostalCode = user.DefaultPostalCode,
                DefaultCountry = string.IsNullOrWhiteSpace(user.DefaultCountry) ? "Azerbaijan" : user.DefaultCountry
            },
            Password = passwordOverride ?? new ChangePasswordInputModel()
        };
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            var localized = error.Code switch
            {
                "DuplicateEmail" or "DuplicateUserName" => _localizer["Identity.DuplicateEmail"].Value,
                "PasswordMismatch" => _localizer["Identity.IncorrectPassword"].Value,
                "PasswordTooShort" => _localizer["Identity.PasswordTooShort"].Value,
                "PasswordRequiresDigit" => _localizer["Identity.PasswordRequiresDigit"].Value,
                "PasswordRequiresLower" => _localizer["Identity.PasswordRequiresLower"].Value,
                "PasswordRequiresUpper" => _localizer["Identity.PasswordRequiresUpper"].Value,
                "PasswordRequiresNonAlphanumeric" => _localizer["Identity.PasswordRequiresSymbol"].Value,
                _ => error.Description
            };

            ModelState.AddModelError(string.Empty, localized);
        }
    }
}
