using System.Net;
using System.Reflection;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class OrganizationControllersTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public OrganizationControllersTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/JobFamilies")]
    [InlineData("/JobFamilies/Create")]
    [InlineData("/Seniorities")]
    [InlineData("/Seniorities/Create")]
    [InlineData("/Positions")]
    [InlineData("/Positions/Create")]
    public async Task KorumaliUclar_AnonimKullaniciyiReddeder(string path)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/JobFamilies")]
    [InlineData("/Seniorities")]
    [InlineData("/Positions")]
    public async Task KorumaliUclar_AdminOlmayanRoluReddeder(string path)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/JobFamilies/Create")]
    [InlineData("/JobFamilies/Deactivate/1")]
    [InlineData("/Seniorities/Create")]
    [InlineData("/Seniorities/Deactivate/1")]
    [InlineData("/Positions/Create")]
    [InlineData("/Positions/Deactivate/1")]
    public async Task MutasyonUclari_AntiforgeryTokenOlmadanReddeder(string path)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
        request.Content = new FormUrlEncodedContent([]);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Theory]
    [InlineData(typeof(JobFamiliesController))]
    [InlineData(typeof(SenioritiesController))]
    [InlineData(typeof(PositionsController))]
    public void Controller_AdminOnlyPolicySiniTasir(Type controllerType)
    {
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
