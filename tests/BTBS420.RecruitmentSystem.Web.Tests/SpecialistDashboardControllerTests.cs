using System.Net;
using System.Reflection;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class SpecialistDashboardControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SpecialistDashboardControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Index_AnonimKullaniciyiReddeder()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/SpecialistDashboard");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(SystemRoles.Admin)]
    [InlineData(SystemRoles.HiringManager)]
    [InlineData(SystemRoles.Candidate)]
    public async Task Index_UzmanOlmayanRoluReddeder(string role)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/SpecialistDashboard");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void Controller_RecruitmentSpecialistOnlyPolicySiniTasir()
    {
        var controllerType = typeof(SpecialistDashboardController);
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorizeAttribute);
        Assert.Equal(AuthorizationPolicies.RecruitmentSpecialistOnly, authorizeAttribute.Policy);
    }

    private HttpClient CreateClient()
    {
        return _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });
    }
}
