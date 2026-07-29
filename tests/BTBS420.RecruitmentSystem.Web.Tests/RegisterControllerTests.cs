using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Controllers;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class RegisterControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public RegisterControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_AnonimKullaniciyaFormGosterir()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/Account/Register");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"UserName\"", content);
        Assert.Contains("name=\"Email\"", content);
        Assert.Contains("name=\"Password\"", content);
        Assert.Contains("name=\"ConfirmPassword\"", content);
        Assert.DoesNotContain("name=\"Role\"", content);
        Assert.DoesNotContain("name=\"RoleId\"", content);
        Assert.DoesNotContain("name=\"IsAdmin\"", content);
    }

    [Fact]
    public async Task Register_ZatenAuthenticatedKullaniciyiAnaSayfayaYonlendirir()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Account/Register");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Register_BosAlanlarModelStateHatasiVeriverir()
    {
        using var client = CreateClient();
        var token = await GetAntiforgeryTokenAsync(client);
        using var content = CreateFormContent(
            token,
            userName: string.Empty,
            email: string.Empty,
            password: string.Empty,
            confirmPassword: string.Empty);

        var response = await client.PostAsync("/Account/Register", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("zorunludur", body);
    }

    [Fact]
    public async Task Register_ParolaTekrariUyusmazsaReddeder()
    {
        using var client = CreateClient();
        var token = await GetAntiforgeryTokenAsync(client);
        using var content = CreateFormContent(
            token,
            userName: "test-kullanici",
            email: "test@example.test",
            password: "Gecerli-Parola-1!",
            confirmPassword: "Farkli-Parola-1!");

        var response = await client.PostAsync("/Account/Register", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Parolalar", body);
    }

    [Fact]
    public async Task Register_AntiforgeryTokenOlmadanReddeder()
    {
        using var client = CreateClient();
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["UserName"] = "herhangi-biri",
                ["Email"] = "herhangi@example.test",
                ["Password"] = "Herhangi-Bir-Parola-1!",
                ["ConfirmPassword"] = "Herhangi-Bir-Parola-1!"
            });

        var response = await client.PostAsync("/Account/Register", content);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public void RegisterViewModel_HicbirRolVeyaYetkiAlaniIcermez()
    {
        var properties = typeof(RegisterViewModel)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.Equal(
            ["UserName", "Email", "Password", "ConfirmPassword"],
            properties);
        Assert.DoesNotContain(
            properties,
            name =>
                name.Contains("Role", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Admin", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Recruiter", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Manager", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Controller_RegisterSozlesmeleriniTasir()
    {
        var controllerType = typeof(AccountController);
        var registerGet = controllerType
            .GetMethods()
            .Single(
                method =>
                    method.Name == nameof(AccountController.Register) &&
                    method.GetCustomAttribute<HttpGetAttribute>() is not null);
        var registerPost = controllerType
            .GetMethods()
            .Single(
                method =>
                    method.Name == nameof(AccountController.Register) &&
                    method.GetCustomAttribute<HttpPostAttribute>() is not null);

        Assert.NotNull(registerGet.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(registerPost.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(registerPost.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.Equal(
            [typeof(RegisterViewModel), typeof(CancellationToken)],
            registerPost.GetParameters().Select(parameter => parameter.ParameterType));
    }

    [Fact]
    public void ApplicationDbContext_NormalizedEmailIcinUniqueIndexTanimliDir()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entityType = context.Model.FindEntityType(typeof(ApplicationUser));
        var emailIndex = entityType?
            .GetIndexes()
            .SingleOrDefault(index => index.GetDatabaseName() == "EmailIndex");

        Assert.NotNull(emailIndex);
        Assert.True(emailIndex.IsUnique);
        Assert.Equal(
            [nameof(ApplicationUser.NormalizedEmail)],
            emailIndex.Properties.Select(property => property.Name));
        Assert.Equal("[NormalizedEmail] IS NOT NULL", emailIndex.GetFilter());
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
        var response = await client.GetAsync("/Account/Register");
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
        string userName,
        string email,
        string password,
        string confirmPassword)
    {
        return new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["UserName"] = userName,
                ["Email"] = email,
                ["Password"] = password,
                ["ConfirmPassword"] = confirmPassword
            });
    }
}
