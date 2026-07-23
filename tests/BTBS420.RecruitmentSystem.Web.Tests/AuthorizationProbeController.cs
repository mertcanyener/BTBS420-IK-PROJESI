using BTBS420.RecruitmentSystem.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTBS420.RecruitmentSystem.Web.Tests;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("_test/authorization")]
public sealed class AuthorizationProbeController : ControllerBase
{
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpGet("admin")]
    public IActionResult AdminOnly()
    {
        return NoContent();
    }
}
