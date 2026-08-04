using System.Net;
using System.Reflection;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class ApplicationsPoolControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ApplicationsPoolControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task KorumaliUc_AnonimKullaniciyiReddeder()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/ApplicationsPool");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task KorumaliUc_AdayRolunuReddeder()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/ApplicationsPool");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddNote_AntiforgeryTokenOlmadanReddeder()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/ApplicationsPool/AddNote/1");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["body"] = "Test notu" });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public void Controller_RecruitmentStaffOnlyPolicySiniKullanir()
    {
        var controllerType = typeof(ApplicationsPoolController);
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorizeAttribute);
        Assert.Equal(AuthorizationPolicies.RecruitmentStaffOnly, authorizeAttribute.Policy);
    }

    [Fact]
    public void AddNote_AntiforgeryTokenDogrulamasiIcerir()
    {
        var controllerType = typeof(ApplicationsPoolController);
        var postMethod = controllerType
            .GetMethods()
            .Single(
                method =>
                    method.Name == nameof(ApplicationsPoolController.AddNote) &&
                    method.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpPostAttribute>() is not null);

        Assert.NotNull(
            postMethod.GetCustomAttribute<Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public async Task CreateInterview_YoneticiRolunuReddeder()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/ApplicationsPool/CreateInterview/1");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.HiringManager);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateInterview_AntiforgeryTokenOlmadanReddeder()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/ApplicationsPool/CreateInterview/1");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["InterviewType"] = "online" });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Theory]
    [InlineData(nameof(ApplicationsPoolController.CreateInterview))]
    public void CreateInterview_YalnizAdminVeUzmanRolleriniKabulEder(string actionName)
    {
        var controllerType = typeof(ApplicationsPoolController);
        var getMethod = controllerType
            .GetMethods()
            .Single(
                method =>
                    method.Name == actionName &&
                    method.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpGetAttribute>() is not null);

        var authorizeAttribute = getMethod.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorizeAttribute);
        Assert.Equal(
            $"{SystemRoles.Admin},{SystemRoles.RecruitmentSpecialist}",
            authorizeAttribute.Roles);
    }

    [Fact]
    public async Task AssignParticipants_YoneticiRolunuReddeder()
    {
        using var client = CreateClient();

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Get, "/ApplicationsPool");
        tokenRequest.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.HiringManager);
        var tokenResponse = await client.SendAsync(tokenRequest);
        var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
        var tokenMatch = System.Text.RegularExpressions.Regex.Match(
            tokenContent,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(tokenMatch.Success, "Antiforgery form alanı bulunamadı.");
        var token = System.Net.WebUtility.HtmlDecode(tokenMatch.Groups[1].Value);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/ApplicationsPool/AssignParticipants/1");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.HiringManager);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["interviewId"] = "1",
                ["__RequestVerificationToken"] = token
            });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AssignParticipants_AntiforgeryTokenOlmadanReddeder()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/ApplicationsPool/AssignParticipants/1");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["interviewId"] = "1" });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public void AssignParticipants_YalnizAdminVeUzmanRolleriniKabulEder()
    {
        var controllerType = typeof(ApplicationsPoolController);
        var postMethod = controllerType
            .GetMethods()
            .Single(
                method =>
                    method.Name == nameof(ApplicationsPoolController.AssignParticipants) &&
                    method.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpPostAttribute>() is not null);

        var authorizeAttribute = postMethod.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorizeAttribute);
        Assert.Equal(
            $"{SystemRoles.Admin},{SystemRoles.RecruitmentSpecialist}",
            authorizeAttribute.Roles);

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
