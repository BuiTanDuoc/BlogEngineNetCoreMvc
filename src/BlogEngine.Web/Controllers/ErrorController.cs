using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BlogEngine.Web.Controllers;

public class ErrorController : Controller
{
    [IgnoreAntiforgeryToken]
    [Route("error/{code:int?}")]
    public IActionResult Index(int? code)
    {
        var status = code ?? 500;
        Response.StatusCode = status;
        ViewBag.RequestId = HttpContext.TraceIdentifier;
        ViewBag.Status = status;

        // Không rò rỉ chi tiết ngoại lệ ra ngoài; chi tiết nằm trong log.
        _ = HttpContext.Features.Get<IExceptionHandlerFeature>();

        return status == 404 ? View("NotFound") : View("Error");
    }
}
