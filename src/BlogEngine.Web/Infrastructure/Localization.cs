using System.Globalization;
using BlogEngine.Web.Services;
using Microsoft.AspNetCore.Localization;

namespace BlogEngine.Web.Infrastructure;

public static class SupportedLanguages
{
    public static readonly string[] Codes = { "en", "vi" };

    public static readonly CultureInfo[] Cultures = Codes.Select(c => new CultureInfo(c)).ToArray();

    public static string DisplayName(string code) => code switch
    {
        "vi" => "Tiếng Việt",
        _ => "English"
    };

    public static bool IsSupported(string? code) => code is not null && Codes.Contains(code, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Lấy ngôn ngữ mặc định từ Settings (Admin → Settings → Default language) khi người dùng chưa chọn.
/// Chạy sau CookieRequestCultureProvider nên lựa chọn của người dùng luôn được ưu tiên.
/// </summary>
public class SettingsRequestCultureProvider : RequestCultureProvider
{
    public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        var settings = await httpContext.RequestServices.GetRequiredService<ISettingsService>().GetAsync();
        var code = (settings.Culture ?? "en").Split('-')[0].ToLowerInvariant();
        if (!SupportedLanguages.IsSupported(code)) code = "en";
        return new ProviderCultureResult(code);
    }
}

/// <summary>
/// .NET tự gán sẵn thông báo tiếng Anh cho EmailAddress/Url; MVC dùng chính câu đó làm khoá dịch.
/// Liệt kê ở đây (trong comment) để tools/check-translations.py biết các khoá này cần có bản dịch:
///   L["The {0} field is not a valid e-mail address."]
///   L["The {0} field is not a valid fully-qualified http, https, or ftp URL."]
/// Mọi attribute khác (Required, StringLength, Range, Compare) đã gán ErrorMessage tường minh trong model.
/// </summary>
internal static class FrameworkValidationMessages { }
