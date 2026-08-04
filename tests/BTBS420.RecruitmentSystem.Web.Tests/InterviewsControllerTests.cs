using System.Net;
using System.Reflection;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class InterviewsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public InterviewsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task KorumaliUc_AnonimKullaniciyiReddeder()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Interviews");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Edit_AdayRolunuReddeder()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Interviews/Edit/1");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Edit_YoneticiRolunuReddeder()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Interviews/Edit/1");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.HiringManager);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Edit_AntiforgeryTokenOlmadanReddeder()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Interviews/Edit/1");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["InterviewType"] = "online" });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public void Controller_AuthorizeAttributeIcerirRolKisitlamazsizin()
    {
        var controllerType = typeof(InterviewsController);
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorizeAttribute);
        Assert.Null(authorizeAttribute.Roles);
        Assert.Null(authorizeAttribute.Policy);
    }

    [Fact]
    public void Edit_YalnizAdminVeUzmanRolleriniKabulEder()
    {
        var controllerType = typeof(InterviewsController);
        var getMethod = controllerType
            .GetMethods()
            .Single(
                method =>
                    method.Name == nameof(InterviewsController.Edit) &&
                    method.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpGetAttribute>() is not null);

        var authorizeAttribute = getMethod.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorizeAttribute);
        Assert.Equal(
            $"{SystemRoles.Admin},{SystemRoles.RecruitmentSpecialist}",
            authorizeAttribute.Roles);
    }

    [Fact]
    public void Edit_AntiforgeryTokenDogrulamasiIcerir()
    {
        var controllerType = typeof(InterviewsController);
        var postMethod = controllerType
            .GetMethods()
            .Single(
                method =>
                    method.Name == nameof(InterviewsController.Edit) &&
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
