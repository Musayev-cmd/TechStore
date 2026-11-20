using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Data;

namespace TechStore.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = DbSeeder.AdminRole)]
public abstract class AdminBaseController : Controller
{
    protected void SetSuccessNotice(string message) => TempData["AdminSuccess"] = message;
    protected void SetErrorNotice(string message) => TempData["AdminError"] = message;
    protected void SetWarningNotice(string message) => TempData["AdminWarning"] = message;
}
