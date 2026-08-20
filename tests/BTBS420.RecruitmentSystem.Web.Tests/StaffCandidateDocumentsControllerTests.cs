using System.Net;
using System.Reflection;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class StaffCandidateDocumentsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public StaffCandidateDocumentsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task KorumaliUc_AnonimKullaniciyiReddeder()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/StaffCandidateDocuments/Index?candidateProfileId=1");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task KorumaliUc_AdayRolunuReddeder()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/StaffCandidateDocuments/Index?candidateProfileId=1");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(SystemRoles.Admin)]
    [InlineData(SystemRoles.RecruitmentSpecialist)]
    [InlineData(SystemRoles.HiringManager)]
    public async Task KorumaliUc_YetkiliPersonelRolleriniKabulEder(string role)
    {
        // Gerçek controller veritabanına eriştiği için (bu test host'unda yok),
        // yalnızca RecruitmentStaffOnly policy'sinin rolü kabul ettiğini
        // AuthorizationProbeController üzerinden doğruluyoruz.
        using var client = CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/_test/authorization/recruitment-staff");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public void Controller_RecruitmentStaffOnlyPolicySiniKullanir()
    {
        var controllerType = typeof(StaffCandidateDocumentsController);
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorizeAttribute);
        Assert.Equal(AuthorizationPolicies.RecruitmentStaffOnly, authorizeAttribute.Policy);
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
