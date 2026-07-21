using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class ErrorController : Controller
{
    [HttpGet("Error/{statusCode:int}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult StatusCodePage(int statusCode)
    {
        var supportedStatusCode = statusCode is StatusCodes.Status403Forbidden
            or StatusCodes.Status404NotFound
            or StatusCodes.Status500InternalServerError;

        var responseStatusCode = supportedStatusCode
            ? statusCode
            : StatusCodes.Status500InternalServerError;

        Response.StatusCode = responseStatusCode;

        return View($"~/Views/Error/{responseStatusCode}.cshtml");
    }
}
