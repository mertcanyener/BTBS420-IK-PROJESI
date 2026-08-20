using System.Net;
using System.Reflection;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class ActivityLogsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ActivityLogsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/ActivityLogs")]
    [InlineData("/ActivityLogs/Export")]
    public async Task Uclar_AnonimKullaniciyiReddeder(string path)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(SystemRoles.RecruitmentSpecialist)]
    [InlineData(SystemRoles.HiringManager)]
    [InlineData(SystemRoles.Candidate)]
    public async Task Index_AdminOlmayanRoluReddeder(string role)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/ActivityLogs");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(SystemRoles.RecruitmentSpecialist)]
    [InlineData(SystemRoles.HiringManager)]
    [InlineData(SystemRoles.Candidate)]
    public async Task Export_AdminOlmayanRoluReddeder(string role)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/ActivityLogs/Export");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void Controller_AdminOnlyPolicySiniTasir()
    {
        var controllerType = typeof(ActivityLogsController);
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorizeAttribute);
        Assert.Equal(AuthorizationPolicies.AdminOnly, authorizeAttribute.Policy);
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
