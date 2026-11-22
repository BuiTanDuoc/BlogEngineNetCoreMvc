using BlogEngine.Web.Infrastructure;
using BlogEngine.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlogEngine.Web.Areas.Admin.Controllers;

[Authorize(Policy = "Admin")]
public class SettingsController : AdminControllerBase
{
    private readonly ISettingsService _settings;
    public SettingsController(ISettingsService settings) => _settings = settings;

    [HttpGet]
    public async Task<IActionResult> Index() => View(await _settings.GetAsync());

    [HttpPost]
    public async Task<IActionResult> Index(BlogSettings model)
    {
        if (!SupportedLanguages.IsSupported(model.Culture))
            ModelState.AddModelError(nameof(model.Culture), L["Unsupported language."]);

        if (!SiteThemes.IsValid(model.Theme))
            ModelState.AddModelError(nameof(model.Theme), L["Unknown theme."]);

        try { _ = TimeZoneInfo.FindSystemTimeZoneById(model.TimeZoneId); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        { ModelState.AddModelError(nameof(model.TimeZoneId), L["Unknown time zone id."]); }

        if (!ModelState.IsValid) return View(model);

        // Null từ model binder (ô trống) -> chuỗi rỗng để không lưu NULL vào Settings.
        model.Email ??= "";
        model.Description ??= "";
        model.EmailSubjectPrefix ??= "";
        model.ContactFormMessage ??= "";
        model.ContactThankMessage ??= "";
        model.TrackingScript ??= "";
        model.SocialFacebook ??= "";
        model.SocialYoutube ??= "";
        model.SocialChat ??= "";

        await _settings.SaveAsync(model);
        Flash(L["Settings saved."]);
        return RedirectToAction(nameof(Index));
    }
}
