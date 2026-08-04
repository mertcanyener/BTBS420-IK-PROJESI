using System.Net;
using System.Reflection;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class JobPostingsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public JobPostingsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("GET", "/JobPostings")]
    [InlineData("GET", "/JobPostings/Create")]
    [InlineData("GET", "/JobPostings/Edit/1")]
    public async Task KorumaliUclar_AnonimKullaniciyiReddeder(string method, string path)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/JobPostings")]
    [InlineData("GET", "/JobPostings/Create")]
    public async Task KorumaliUclar_AdayRoluReddeder(string method, string path)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/JobPostings/Create")]
    [InlineData("GET", "/JobPostings/Edit/1")]
    public async Task KorumaliUclar_IseAlimYoneticisiRoluMutasyonUclariniReddeder(string method, string path)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.HiringManager);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/JobPostings/Create")]
    [InlineData("/JobPostings/Edit/1")]
    public async Task MutasyonUclari_AntiforgeryTokenOlmadanReddeder(string path)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
        request.Content = new FormUrlEncodedContent([]);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public void Controller_UcRolunClassSeviyesindeErisimineIzinVerir()
    {
        var controllerType = typeof(JobPostingsController);
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorizeAttribute);
        Assert.Equal(
            $"{SystemRoles.Admin},{SystemRoles.RecruitmentSpecialist},{SystemRoles.HiringManager}",
            authorizeAttribute.Roles);
    }

    [Fact]
    public void MutasyonAction_AdminVeIseAlimUzmaniIleSinirlanir()
    {
        var controllerType = typeof(JobPostingsController);

        foreach (var actionName in new[] { "Create", "Edit" })
        {
            var postMethod = controllerType
                .GetMethods()
                .Single(
                    method =>
                        method.Name == actionName &&
                        method.GetCustomAttribute<HttpPostAttribute>() is not null);

            Assert.NotNull(
                postMethod.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());

            foreach (var method in controllerType
                .GetMethods()
                .Where(candidate => candidate.Name == actionName))
            {
                var methodAuthorize = method.GetCustomAttribute<AuthorizeAttribute>();

                Assert.NotNull(methodAuthorize);
                Assert.Equal(
                    $"{SystemRoles.Admin},{SystemRoles.RecruitmentSpecialist}",
                    methodAuthorize.Roles);
            }
        }
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
