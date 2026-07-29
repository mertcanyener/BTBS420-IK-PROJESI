using System.Net;
using System.Reflection;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class CandidateFilterCatalogControllersTests :
    IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public CandidateFilterCatalogControllersTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/Skills")]
    [InlineData("/Skills/Create")]
    [InlineData("/Educations")]
    [InlineData("/Educations/Create")]
    [InlineData("/Languages")]
    [InlineData("/Languages/Create")]
    [InlineData("/Locations")]
    [InlineData("/Locations/Create")]
    [InlineData("/ExperienceRanges")]
    [InlineData("/ExperienceRanges/Create")]
    public async Task KorumaliUclar_AnonimKullaniciyiReddeder(string path)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/Skills")]
    [InlineData("/Educations")]
    [InlineData("/Languages")]
    [InlineData("/Locations")]
    [InlineData("/ExperienceRanges")]
    public async Task KorumaliUclar_AdminOlmayanRoluReddeder(string path)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/Skills/Create")]
    [InlineData("/Skills/Deactivate/1")]
    [InlineData("/Educations/Create")]
    [InlineData("/Educations/Deactivate/1")]
    [InlineData("/Languages/Create")]
    [InlineData("/Languages/Deactivate/1")]
    [InlineData("/Locations/Create")]
    [InlineData("/Locations/Deactivate/1")]
    [InlineData("/ExperienceRanges/Create")]
    [InlineData("/ExperienceRanges/Deactivate/1")]
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
    [InlineData(typeof(SkillsController))]
    [InlineData(typeof(EducationsController))]
    [InlineData(typeof(LanguagesController))]
    [InlineData(typeof(LocationsController))]
    [InlineData(typeof(ExperienceRangesController))]
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
