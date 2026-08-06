using System.Net;
using System.Reflection;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class ManagerDashboardControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ManagerDashboardControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Index_AnonimKullaniciyiReddeder()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/ManagerDashboard");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(SystemRoles.Admin)]
    [InlineData(SystemRoles.RecruitmentSpecialist)]
    [InlineData(SystemRoles.Candidate)]
    public async Task Index_YoneticiOlmayanRoluReddeder(string role)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/ManagerDashboard");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void Controller_HiringManagerOnlyPolicySiniTasir()
    {
        var controllerType = typeof(ManagerDashboardController);
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorizeAttribute);
        Assert.Equal(AuthorizationPolicies.HiringManagerOnly, authorizeAttribute.Policy);
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
