using System.Net;
using System.Security.Claims;
using BTBS420.RecruitmentSystem.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class AuthorizationPolicyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuthorizationPolicyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<string, string, string> RolePolicies =>
        new()
        {
            {
                AuthorizationPolicies.AdminOnly,
                SystemRoles.Admin,
                SystemRoles.Candidate
            },
            {
                AuthorizationPolicies.RecruitmentSpecialistOnly,
                SystemRoles.RecruitmentSpecialist,
                SystemRoles.Candidate
            },
            {
                AuthorizationPolicies.HiringManagerOnly,
                SystemRoles.HiringManager,
                SystemRoles.Candidate
            },
            {
                AuthorizationPolicies.CandidateOnly,
                SystemRoles.Candidate,
                SystemRoles.HiringManager
            }
        };

    public static TheoryData<string> PolicyNames =>
        new()
        {
            AuthorizationPolicies.AdminOnly,
            AuthorizationPolicies.RecruitmentSpecialistOnly,
            AuthorizationPolicies.HiringManagerOnly,
            AuthorizationPolicies.CandidateOnly
        };

    [Theory]
    [MemberData(nameof(RolePolicies))]
    public async Task RolPolitikasi_DogruRoleIzinVerir(
        string policyName,
        string allowedRole,
        string _)
    {
        var authorizationService =
            _factory.Services.GetRequiredService<IAuthorizationService>();
        var user = CreateAuthenticatedUser(allowedRole);

        var result = await authorizationService.AuthorizeAsync(
            user,
            resource: null,
            policyName);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [MemberData(nameof(RolePolicies))]
    public async Task RolPolitikasi_YanlisRoluReddeder(
        string policyName,
        string _,
        string deniedRole)
    {
        var authorizationService =
            _factory.Services.GetRequiredService<IAuthorizationService>();
        var user = CreateAuthenticatedUser(deniedRole);

        var result = await authorizationService.AuthorizeAsync(
            user,
            resource: null,
            policyName);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [MemberData(nameof(PolicyNames))]
    public async Task RolPolitikasi_AnonimKullaniciyiReddeder(
        string policyName)
    {
        var authorizationService =
            _factory.Services.GetRequiredService<IAuthorizationService>();
        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await authorizationService.AuthorizeAsync(
            anonymousUser,
            resource: null,
            policyName);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task PolicyKorumaliEndpoint_AdminRoluneIzinVerir()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/_test/authorization/admin");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task PolicyKorumaliEndpoint_YanlisRoluReddeder()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/_test/authorization/admin");
        request.Headers.Add(
            TestAuthenticationHandler.RoleHeaderName,
            SystemRoles.Candidate);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PolicyKorumaliEndpoint_AnonimKullaniciyiReddeder()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/_test/authorization/admin");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private HttpClient CreateClient()
    {
        return _factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    private static ClaimsPrincipal CreateAuthenticatedUser(string roleName)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "kan22-policy-test-user"),
                new Claim(ClaimTypes.Role, roleName)
            ],
            TestAuthenticationHandler.SchemeName);

        return new ClaimsPrincipal(identity);
    }
}
