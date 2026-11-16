using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BlogEngine.Web.Services;

public static partial class SlugHelper
{
    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlnum();

    /// <summary>Tạo slug ASCII, bỏ dấu (kể cả tiếng Việt: "Đường đi" → "duong-di").</summary>
    public static string Slugify(string? text, int maxLength = 100)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        var lower = text.Trim().ToLowerInvariant().Replace('đ', 'd');
        var decomposed = lower.Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var slug = NonAlnum().Replace(sb.ToString(), "-").Trim('-');
        if (slug.Length > maxLength) slug = slug[..maxLength].Trim('-');
        return slug;
    }

    /// <summary>Thêm hậu tố -2, -3, ... cho đến khi slug chưa tồn tại.</summary>
    public static async Task<string> EnsureUniqueAsync(string baseSlug, Func<string, Task<bool>> exists, string fallbackPrefix = "item")
    {
        if (string.IsNullOrEmpty(baseSlug))
            baseSlug = $"{fallbackPrefix}-{Guid.NewGuid().ToString("N")[..8]}";

        var slug = baseSlug;
        var i = 2;
        while (await exists(slug))
            slug = $"{baseSlug}-{i++}";
        return slug;
    }
}
