using BlogEngine.Web.Infrastructure;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace BlogEngine.Web.Controllers;

public class LanguageController : Controller
{
    /// <summary>Đổi ngôn ngữ giao diện: lưu cookie rồi quay lại trang trước.</summary>
    [HttpGet("lang/{culture}")]
    public IActionResult Set(string culture, string? returnUrl = null)
    {
        if (!SupportedLanguages.IsSupported(culture)) return BadRequest();

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture.ToLowerInvariant())),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps
            });

        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : Redirect("/");
    }
}
