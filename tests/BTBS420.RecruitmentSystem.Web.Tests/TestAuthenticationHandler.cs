using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BTBS420.RecruitmentSystem.Web.Tests;

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "Kan22Test";
    internal const string RoleHeaderName = "X-Test-Role";
    internal const string UserIdHeaderName = "X-Test-User-Id";
    internal const string DefaultUserId = "kan22-test-user";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var roleName = Request.Headers[RoleHeaderName].ToString();

        if (string.IsNullOrWhiteSpace(roleName))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var requestedUserId = Request.Headers[UserIdHeaderName].ToString();
        var userId = string.IsNullOrWhiteSpace(requestedUserId)
            ? DefaultUserId
            : requestedUserId.Trim();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, "KAN-22 Test User"),
            new Claim(ClaimTypes.Role, roleName)
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
