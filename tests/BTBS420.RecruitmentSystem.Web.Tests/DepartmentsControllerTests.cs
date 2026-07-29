using System.Net;
using System.Reflection;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class DepartmentsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public DepartmentsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("GET", "/Departments")]
    [InlineData("GET", "/Departments/Create")]
    [InlineData("GET", "/Departments/Edit/1")]
    public async Task KorumaliUclar_AnonimKullaniciyiReddeder(string method, string path)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/Departments")]
    [InlineData("GET", "/Departments/Create")]
    public async Task KorumaliUclar_AdminOlmayanRoluReddeder(string method, string path)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/Departments/Create")]
    [InlineData("/Departments/Edit/1")]
    [InlineData("/Departments/Deactivate/1")]
    [InlineData("/Departments/Activate/1")]
    public async Task MutasyonUclari_AntiforgeryTokenOlmadanReddeder(string path)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
        request.Content = new FormUrlEncodedContent([]);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public void Controller_AdminOnlyPolicySiniTasir()
    {
        var controllerType = typeof(DepartmentsController);
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorizeAttribute);
        Assert.Equal(AuthorizationPolicies.AdminOnly, authorizeAttribute.Policy);

        foreach (var actionName in new[] { "Create", "Edit", "Deactivate", "Activate" })
        {
            var postMethod = controllerType
                .GetMethods()
                .Single(
                    method =>
                        method.Name == actionName &&
                        method.GetCustomAttribute<HttpPostAttribute>() is not null);

            Assert.NotNull(
                postMethod.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
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
