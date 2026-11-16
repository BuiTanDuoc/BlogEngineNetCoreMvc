using BlogEngine.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BlogEngine.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Staff")]
public abstract class AdminControllerBase : LocalizedController
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // Tài khoản được tạo với mật khẩu tạm phải đổi mật khẩu trước khi dùng khu quản trị.
        if (User.HasClaim(c => c.Type == ClaimsExtensions.MustChangePasswordClaim))
        {
            context.Result = RedirectToAction("ChangePassword", "Account", new { area = "", returnUrl = "/admin" });
            return;
        }
        base.OnActionExecuting(context);
    }

    protected void Flash(string message) => TempData["Message"] = message;
    protected void FlashError(string message) => TempData["Error"] = message;

    protected IActionResult BackTo(string action, object? routeValues = null)
        => RedirectToAction(action, routeValues);
}
