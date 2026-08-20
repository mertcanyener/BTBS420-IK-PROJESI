using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class AccountControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AccountControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_AnonimKullaniciyaFormGosterir()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/Account/Login");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("for=\"UsernameOrEmail\"", content);
        Assert.Contains("veya e-posta", content);
        Assert.Contains("name=\"Password\"", content);
        Assert.DoesNotContain("value=\"Kan31\"", content);
    }

    [Fact]
    public async Task Login_ZatenAuthenticatedKullaniciyiAnaSayfayaYonlendirir()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Account/Login");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Login_BosAlanlarModelStateHatasiVeriverir()
    {
        using var client = CreateClient();
        var token = await GetAntiforgeryTokenAsync(client);
        using var content = CreateFormContent(
            token,
            usernameOrEmail: string.Empty,
            password: string.Empty);

        var response = await client.PostAsync("/Account/Login", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("zorunludur", body);
    }

    [Fact]
    public async Task Login_AntiforgeryTokenOlmadanReddeder()
    {
        using var client = CreateClient();
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["UsernameOrEmail"] = "herhangi-biri",
                ["Password"] = "herhangi-bir-parola"
            });

        var response = await client.PostAsync("/Account/Login", content);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Logout_AuthenticateOlmamisKullaniciyiReddeder()
    {
        using var client = CreateClient();
        using var content = new FormUrlEncodedContent([]);

        var response = await client.PostAsync("/Account/Logout", content);

        // [Authorize] filtresi antiforgery kontrolünden önce çalışır, bu yüzden
        // kimliği doğrulanmamış istek antiforgery token eksikliğine bakılmaksızın
        // doğrudan 401 ile reddedilir.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Controller_LoginVeLogoutSozlesmeleriniTasir()
    {
        var controllerType = typeof(AccountController);
        var loginGet = controllerType
            .GetMethods()
            .Single(
                method =>
                    method.Name == nameof(AccountController.Login) &&
                    method.GetCustomAttribute<HttpGetAttribute>() is not null);
        var loginPost = controllerType
            .GetMethods()
            .Single(
                method =>
                    method.Name == nameof(AccountController.Login) &&
                    method.GetCustomAttribute<HttpPostAttribute>() is not null);
        var logout = controllerType.GetMethod(nameof(AccountController.Logout))!;

        Assert.NotNull(loginGet.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(loginPost.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(loginPost.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());

        Assert.NotNull(logout.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(logout.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(logout.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.DoesNotContain(
            logout.GetCustomAttributes<HttpGetAttribute>(),
            _ => true);
    }

    private HttpClient CreateClient()
    {
        return _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true
            });
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/Account/Login");
        var content = await response.Content.ReadAsStringAsync();

        var tokenMatch = Regex.Match(
            content,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(tokenMatch.Success, "Antiforgery form alanı bulunamadı.");

        return WebUtility.HtmlDecode(tokenMatch.Groups[1].Value);
    }

    private static FormUrlEncodedContent CreateFormContent(
        string antiforgeryToken,
        string usernameOrEmail,
        string password,
        string? returnUrl = null)
    {
        return new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["UsernameOrEmail"] = usernameOrEmail,
                ["Password"] = password,
                ["ReturnUrl"] = returnUrl ?? string.Empty
            });
    }
}
