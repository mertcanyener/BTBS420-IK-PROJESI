using System.Net;
using System.Reflection;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class JobApplicationsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public JobApplicationsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task KorumaliUc_AnonimKullaniciyiReddeder()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/JobApplications");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(SystemRoles.Admin)]
    [InlineData(SystemRoles.RecruitmentSpecialist)]
    [InlineData(SystemRoles.HiringManager)]
    public async Task KorumaliUc_AdayOlmayanRolleriReddeder(string role)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/JobApplications");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MutasyonUcu_AntiforgeryTokenOlmadanReddeder()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/JobApplications/Create");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["jobPostingId"] = "1" });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public void Controller_YalnizAdayRolununErisimineIzinVerir()
    {
        var controllerType = typeof(JobApplicationsController);
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorizeAttribute);
        Assert.Equal(SystemRoles.Candidate, authorizeAttribute.Roles);
    }

    [Theory]
    [InlineData(nameof(JobApplicationsController.Create))]
    [InlineData(nameof(JobApplicationsController.Withdraw))]
    public void PostUclari_AntiforgeryTokenDogrulamasiIcerir(string actionName)
    {
        var controllerType = typeof(JobApplicationsController);
        var postMethod = controllerType
            .GetMethods()
            .Single(
                method =>
                    method.Name == actionName &&
                    method.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpPostAttribute>() is not null);

        Assert.NotNull(
            postMethod.GetCustomAttribute<Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public async Task Withdraw_AntiforgeryTokenOlmadanReddeder()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/JobApplications/Withdraw/1");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        request.Content = new FormUrlEncodedContent([]);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
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
