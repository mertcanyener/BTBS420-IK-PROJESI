using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using BTBS420.RecruitmentSystem.Web.Controllers;
using BTBS420.RecruitmentSystem.Web.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class PasswordResetControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PasswordResetControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ForgotPassword_AnonimKullaniciyaFormGosterir()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/Account/ForgotPassword");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"Email\"", content);
    }

    [Fact]
    public async Task ForgotPassword_GecersizEmailModelStateHatasiVeriverir()
    {
        using var client = CreateClient();
        var token = await GetAntiforgeryTokenAsync(client, "/Account/ForgotPassword");
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Email"] = "gecersiz-email"
            });

        var response = await client.PostAsync("/Account/ForgotPassword", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("e-posta adresi", body);
    }

    [Fact]
    public async Task ForgotPassword_AntiforgeryTokenOlmadanReddeder()
    {
        using var client = CreateClient();
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["Email"] = "herhangi@example.test" });

        var response = await client.PostAsync("/Account/ForgotPassword", content);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_AnonimKullaniciyaFormGosterir()
    {
        using var client = CreateClient();

        var response = await client.GetAsync(
            "/Account/ResetPassword?email=test@example.test&token=abc");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"Password\"", content);
        Assert.Contains("name=\"ConfirmPassword\"", content);
        Assert.Contains("value=\"abc\"", content);
    }

    [Fact]
    public async Task ResetPassword_ParolaTekrariUyusmazsaReddeder()
    {
        using var client = CreateClient();
        var token = await GetAntiforgeryTokenAsync(
            client,
            "/Account/ResetPassword?email=test@example.test&token=abc");
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Email"] = "test@example.test",
                ["Token"] = "abc",
                ["Password"] = "Gecerli-Parola-1!",
                ["ConfirmPassword"] = "Farkli-Parola-1!"
            });

        var response = await client.PostAsync("/Account/ResetPassword", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Parolalar", body);
    }

    [Fact]
    public async Task ResetPassword_AntiforgeryTokenOlmadanReddeder()
    {
        using var client = CreateClient();
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Email"] = "test@example.test",
                ["Token"] = "abc",
                ["Password"] = "Herhangi-Bir-Parola-1!",
                ["ConfirmPassword"] = "Herhangi-Bir-Parola-1!"
            });

        var response = await client.PostAsync("/Account/ResetPassword", content);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public void Controller_ForgotPasswordVeResetPasswordSozlesmeleriniTasir()
    {
        var controllerType = typeof(AccountController);
        var forgotPost = controllerType
            .GetMethods()
            .Single(
                method =>
                    method.Name == nameof(AccountController.ForgotPassword) &&
                    method.GetCustomAttribute<HttpPostAttribute>() is not null);
        var resetPost = controllerType
            .GetMethods()
            .Single(
                method =>
                    method.Name == nameof(AccountController.ResetPassword) &&
                    method.GetCustomAttribute<HttpPostAttribute>() is not null);

        Assert.NotNull(forgotPost.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(forgotPost.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.NotNull(resetPost.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(resetPost.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
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

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        var tokenMatch = Regex.Match(
            content,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(tokenMatch.Success, "Antiforgery form alanı bulunamadı.");

        return WebUtility.HtmlDecode(tokenMatch.Groups[1].Value);
    }
}
