using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BlogEngine.Web.Data.Entities;
using BlogEngine.Web.Services;

namespace BlogEngine.Web.Infrastructure;

public static class ClaimsExtensions
{
    public const string MustChangePasswordClaim = "must_change_password";

    public static int GetUserId(this ClaimsPrincipal user)
        => int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public static bool IsAdministrator(this ClaimsPrincipal user) => user.IsInRole(RoleNames.Administrators);

    public static bool IsStaff(this ClaimsPrincipal user)
        => user.IsInRole(RoleNames.Administrators) || user.IsInRole(RoleNames.Editors);

    public static ClaimsPrincipal BuildPrincipal(User user, string scheme)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new("display_name", user.Name)
        };
        claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r.Name)));
        if (user.MustChangePassword) claims.Add(new Claim(MustChangePasswordClaim, "1"));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, scheme));
    }
}

public static partial class TextExtensions
{
    [GeneratedRegex("<[^>]*>")]
    private static partial Regex HtmlTag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"<img\b[^>]*?\bsrc\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex ImgSrc();

    /// <summary>URL ảnh đầu tiên trong nội dung HTML (chỉ nhận http(s) hoặc đường dẫn gốc "/..."); dùng làm thumbnail.</summary>
    public static string? FirstImageUrl(this string? html)
    {
        if (string.IsNullOrEmpty(html)) return null;
        var m = ImgSrc().Match(html);
        if (!m.Success) return null;
        var src = WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
        if (src.StartsWith('/') && !src.StartsWith("//")) return src;
        return src.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || src.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? src : null;
    }

    public static string StripHtml(this string? html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var text = HtmlTag().Replace(html, " ");
        text = WebUtility.HtmlDecode(text);
        return Whitespace().Replace(text, " ").Trim();
    }

    public static string Excerpt(this string? html, int length)
    {
        var text = html.StripHtml();
        if (text.Length <= length) return text;
        var cut = text[..length];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > length / 2) cut = cut[..lastSpace];
        return cut.TrimEnd('.', ',', ';', ':') + "…";
    }

    /// <summary>Comment là văn bản thuần: encode toàn bộ HTML rồi giữ xuống dòng.</summary>
    public static string ToSafeCommentHtml(this string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var encoded = WebUtility.HtmlEncode(text.Trim());
        return encoded.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "<br />");
    }

    /// <summary>Ước lượng thời gian đọc (phút) với tốc độ ~200 từ/phút.</summary>
    public static int ReadingMinutes(this string? html)
    {
        var words = html.StripHtml().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Max(1, (int)Math.Ceiling(words / 200.0));
    }

    public static string? NullIfEmpty(this string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Chỉ cho phép http/https để tránh javascript: URL trong link website của comment.</summary>
    public static string? ToSafeHttpUrl(this string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var candidate = url.Trim();
        if (!candidate.Contains("://")) candidate = "http://" + candidate;
        return Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.ToString()
            : null;
    }

    public static string GravatarUrl(this string? email, int size = 48)
    {
        var normalized = (email ?? "").Trim().ToLowerInvariant();
        var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
        return $"https://www.gravatar.com/avatar/{hash}?s={size}&d=mp";
    }
}

public static class LocalizerExtensions
{
    /// <summary>
    /// Chọn câu số ít/số nhiều (tiếng Anh). {0} luôn là số lượng; tham số bổ sung bắt đầu từ {1}.
    /// Tiếng Việt không có số nhiều nên hai bản dịch giống nhau.
    /// </summary>
    public static Microsoft.Extensions.Localization.LocalizedString Plural(
        this Microsoft.Extensions.Localization.IStringLocalizer localizer, int count, string one, string other, params object[] extra)
        => localizer[count == 1 ? one : other, new object[] { count }.Concat(extra).ToArray()];
}

public static class DateExtensions
{
    public static TimeZoneInfo GetTimeZone(this BlogSettings settings)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }

    public static DateTime ToBlogTime(this DateTime utc, BlogSettings settings)
    {
        if (utc.Kind == DateTimeKind.Unspecified) utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc.ToUniversalTime(), settings.GetTimeZone());
    }

    /// <summary>Khoảng [start, end) theo UTC của một tháng tính theo múi giờ của blog.</summary>
    public static (DateTime StartUtc, DateTime EndUtc) MonthRangeUtc(int year, int month, BlogSettings settings)
    {
        var startLocal = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return RangeUtc(startLocal, startLocal.AddMonths(1), settings);
    }

    public static (DateTime StartUtc, DateTime EndUtc) YearRangeUtc(int year, BlogSettings settings)
    {
        var startLocal = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return RangeUtc(startLocal, startLocal.AddYears(1), settings);
    }

    private static (DateTime, DateTime) RangeUtc(DateTime startLocal, DateTime endLocal, BlogSettings settings)
    {
        var tz = settings.GetTimeZone();
        try
        {
            return (TimeZoneInfo.ConvertTimeToUtc(startLocal, tz), TimeZoneInfo.ConvertTimeToUtc(endLocal, tz));
        }
        catch (ArgumentException)
        {
            // Giờ không hợp lệ do chuyển múi giờ (DST) - dùng UTC.
            return (DateTime.SpecifyKind(startLocal, DateTimeKind.Utc), DateTime.SpecifyKind(endLocal, DateTimeKind.Utc));
        }
    }
}
