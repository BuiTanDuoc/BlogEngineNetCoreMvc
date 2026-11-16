using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace BlogEngine.Web.Infrastructure;

/// <summary>Controller cơ sở có sẵn localizer <c>L</c> (không cần khai báo trong constructor).</summary>
public abstract class LocalizedController : Controller
{
    private IStringLocalizer<SharedResource>? _localizer;

    protected IStringLocalizer<SharedResource> L =>
        _localizer ??= HttpContext.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();
}
