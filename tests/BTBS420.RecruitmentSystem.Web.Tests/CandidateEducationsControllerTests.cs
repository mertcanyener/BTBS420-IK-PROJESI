using System.Net;
using System.Reflection;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class CandidateEducationsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public CandidateEducationsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task KorumaliUc_AnonimKullaniciyiReddeder()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/CandidateEducations");

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
        using var request = new HttpRequestMessage(HttpMethod.Get, "/CandidateEducations");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MutasyonUcu_AntiforgeryTokenOlmadanReddeder()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/CandidateEducations/Create");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        request.Content = new FormUrlEncodedContent([]);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public void Controller_YalnizAdayRolununErisimineIzinVerir()
    {
        var controllerType = typeof(CandidateEducationsController);
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorizeAttribute);
        Assert.Equal(SystemRoles.Candidate, authorizeAttribute.Roles);
    }

    [Theory]
    [InlineData(nameof(CandidateEducationsController.Create))]
    [InlineData(nameof(CandidateEducationsController.Edit))]
    [InlineData(nameof(CandidateEducationsController.Delete))]
    public void PostUclari_AntiforgeryTokenDogrulamasiIcerir(string actionName)
    {
        var controllerType = typeof(CandidateEducationsController);
        var postMethod = controllerType
            .GetMethods()
            .Single(
                method =>
                    method.Name == actionName &&
                    method.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpPostAttribute>() is not null);

        Assert.NotNull(
            postMethod.GetCustomAttribute<Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute>());
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
