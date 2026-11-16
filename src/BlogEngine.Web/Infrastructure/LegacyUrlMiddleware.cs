using Microsoft.AspNetCore.Http.Extensions;

namespace BlogEngine.Web.Infrastructure;

/// <summary>
/// Giữ nguyên link cũ của BlogEngine.NET (SEO): /post/abc.aspx → /post/abc,
/// /default.aspx → /, /syndication.axd → /feed ... bằng redirect 301.
/// </summary>
public class LegacyUrlMiddleware
{
    private readonly RequestDelegate _next;
    public LegacyUrlMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        string? target = null;
        if (path.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase))
        {
            target = path[..^5];
            if (target.EndsWith("/default", StringComparison.OrdinalIgnoreCase) || target.Equals("/default", StringComparison.OrdinalIgnoreCase))
                target = target[..^"default".Length];
            if (target.Length == 0) target = "/";
        }
        else if (path.Equals("/syndication.axd", StringComparison.OrdinalIgnoreCase))
        {
            var format = context.Request.Query["format"].ToString();
            target = format.Equals("atom", StringComparison.OrdinalIgnoreCase) ? "/feed/atom" : "/feed";
            context.Response.Redirect(target, permanent: true);
            return;
        }

        if (target is not null)
        {
            var url = target + context.Request.QueryString.ToUriComponent();
            context.Response.Redirect(url, permanent: true);
            return;
        }

        await _next(context);
    }
}
