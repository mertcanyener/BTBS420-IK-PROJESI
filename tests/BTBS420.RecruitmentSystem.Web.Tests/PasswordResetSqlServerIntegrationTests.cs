using System.Net;
using System.Text.RegularExpressions;
using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class PasswordResetSqlServerIntegrationTests :
    IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN33_TEST_SQLSERVER_CONNECTION_STRING";

    private const string TestPassword = "Kan33-Gecici-Sifre-1!";
    private const string NewPassword = "Kan33-Yeni-Sifre-1!";

    private readonly TestWebApplicationFactory _baseFactory;

    public PasswordResetSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task ForgotPassword_GecerliEmailIleAyniGenelMesajDonulurVeAuditOlusur()
    {
        using var factory = CreateRealAuthenticationFactory();
        var runId = Guid.NewGuid().ToString("N");
        var (userName, email) = await CreateUserAsync(factory, runId);
        using var client = CreateClient(factory);

        var response = await PostForgotPasswordAsync(client, email);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var userId = await GetUserIdAsync(factory, userName);
        await using var context = CreateRawContext();
        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.PasswordResetRequested &&
                    l.TargetEntityId == userId)
            .FirstOrDefaultAsync();

        Assert.NotNull(log);
        Assert.DoesNotContain(email, log.Summary);
    }

    [SqlServerIntegrationFact]
    public async Task ForgotPassword_OlmayanEmailIleAyniMesajDonulurVeAuditOlusmaz()
    {
        using var factory = CreateRealAuthenticationFactory();
        var runId = Guid.NewGuid().ToString("N");
        var (_, existingEmail) = await CreateUserAsync(factory, runId);
        var unknownEmail = $"kan33-yok-{runId}@example.test";
        using var existingClient = CreateClient(factory);
        using var unknownClient = CreateClient(factory);

        var existingResponse = await PostForgotPasswordAsync(existingClient, existingEmail);
        var unknownResponse = await PostForgotPasswordAsync(unknownClient, unknownEmail);

        Assert.Equal(HttpStatusCode.Redirect, existingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, unknownResponse.StatusCode);
        Assert.Equal(
            existingResponse.Headers.Location,
            unknownResponse.Headers.Location);

        await using var context = CreateRawContext();
        var unknownLogCount = await context.ActivityLogs
            .Where(l => l.ActionCode == ActivityActionCodes.PasswordResetRequested)
            .Where(l => l.Summary.Contains(unknownEmail))
            .CountAsync();

        Assert.Equal(0, unknownLogCount);
    }

    [SqlServerIntegrationFact]
    public async Task ResetPassword_GecerliTokenIleParolaGuncellenirVeEskiParolaGecersizOlur()
    {
        using var factory = CreateRealAuthenticationFactory();
        var runId = Guid.NewGuid().ToString("N");
        var (userName, email) = await CreateUserAsync(factory, runId);
        var resetToken = await GenerateResetTokenAsync(factory, userName);
        using var client = CreateClient(factory);

        var response = await PostResetPasswordAsync(client, email, resetToken, NewPassword);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.OriginalString);

        using var scope = factory.Services.CreateScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(userName);
        Assert.NotNull(user);

        var oldPasswordCheck = await userManager.CheckPasswordAsync(user, TestPassword);
        var newPasswordCheck = await userManager.CheckPasswordAsync(user, NewPassword);
        Assert.False(oldPasswordCheck);
        Assert.True(newPasswordCheck);
    }

    [SqlServerIntegrationFact]
    public async Task ResetPassword_GecersizTokenReddedilir()
    {
        using var factory = CreateRealAuthenticationFactory();
        var runId = Guid.NewGuid().ToString("N");
        var (userName, email) = await CreateUserAsync(factory, runId);
        using var client = CreateClient(factory);

        var response = await PostResetPasswordAsync(
            client,
            email,
            "gecersiz-bozuk-token",
            NewPassword);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("olabilir", body);

        using var scope = factory.Services.CreateScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(userName);
        Assert.NotNull(user);
        Assert.True(await userManager.CheckPasswordAsync(user, TestPassword));
    }

    [SqlServerIntegrationFact]
    public async Task ResetPassword_SuresiGecmisTokenReddedilir()
    {
        using var factory = CreateRealAuthenticationFactory(shortTokenLifespan: true);
        var runId = Guid.NewGuid().ToString("N");
        var (userName, email) = await CreateUserAsync(factory, runId);
        var resetToken = await GenerateResetTokenAsync(factory, userName);

        await Task.Delay(750);

        using var client = CreateClient(factory);
        var response = await PostResetPasswordAsync(client, email, resetToken, NewPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(userName);
        Assert.NotNull(user);
        Assert.True(await userManager.CheckPasswordAsync(user, TestPassword));
    }

    [SqlServerIntegrationFact]
    public async Task ResetPassword_AuditKaydiHassasVeriIcermez()
    {
        using var factory = CreateRealAuthenticationFactory();
        var runId = Guid.NewGuid().ToString("N");
        var (userName, email) = await CreateUserAsync(factory, runId);
        var resetToken = await GenerateResetTokenAsync(factory, userName);
        var userId = await GetUserIdAsync(factory, userName);
        using var client = CreateClient(factory);

        var response = await PostResetPasswordAsync(client, email, resetToken, NewPassword);
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(resetToken, body);
        Assert.DoesNotContain(NewPassword, body);

        await using var context = CreateRawContext();
        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.PasswordResetSucceeded &&
                    l.TargetEntityId == userId)
            .FirstOrDefaultAsync();

        Assert.NotNull(log);
        Assert.Equal(ActivityEntityTypes.User, log.TargetEntityType);
        Assert.DoesNotContain(resetToken, log.Summary);
        Assert.DoesNotContain(NewPassword, log.Summary);
    }

    private WebApplicationFactory<Program> CreateRealAuthenticationFactory(
        bool shortTokenLifespan = false)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            ConnectionStringEnvironmentVariable)!;

        return _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = connectionString
                    });
            });
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme =
                        IdentityConstants.ApplicationScheme;
                    options.DefaultChallengeScheme =
                        IdentityConstants.ApplicationScheme;
                    options.DefaultSignInScheme =
                        IdentityConstants.ApplicationScheme;
                    options.DefaultForbidScheme =
                        IdentityConstants.ApplicationScheme;
                });

                if (shortTokenLifespan)
                {
                    services.Configure<DataProtectionTokenProviderOptions>(options =>
                    {
                        options.TokenLifespan = TimeSpan.FromMilliseconds(500);
                    });
                }
            });
        });
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
    {
        return factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true
            });
    }

    private static async Task<(string UserName, string Email)> CreateUserAsync(
        WebApplicationFactory<Program> factory,
        string runId)
    {
        using var scope = factory.Services.CreateScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var userName = $"kan33-user-{runId}";
        var email = $"kan33-{runId}@example.test";

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            IsActive = true
        };

        var result = await userManager.CreateAsync(user, TestPassword);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Test kullanıcısı oluşturulamadı: " +
                string.Join(", ", result.Errors.Select(error => error.Code)));
        }

        return (userName, email);
    }

    private static async Task<string> GetUserIdAsync(
        WebApplicationFactory<Program> factory,
        string userName)
    {
        using var scope = factory.Services.CreateScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(userName)
            ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

        return user.Id;
    }

    private static async Task<string> GenerateResetTokenAsync(
        WebApplicationFactory<Program> factory,
        string userName)
    {
        using var scope = factory.Services.CreateScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(userName)
            ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

        return await userManager.GeneratePasswordResetTokenAsync(user);
    }

    private static async Task<HttpResponseMessage> PostForgotPasswordAsync(
        HttpClient client,
        string email)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/Account/ForgotPassword");
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Email"] = email
            });

        return await client.PostAsync("/Account/ForgotPassword", content);
    }

    private static async Task<HttpResponseMessage> PostResetPasswordAsync(
        HttpClient client,
        string email,
        string resetToken,
        string newPassword)
    {
        var url = $"/Account/ResetPassword?email={Uri.EscapeDataString(email)}" +
            $"&token={Uri.EscapeDataString(resetToken)}";
        var token = await GetAntiforgeryTokenAsync(client, url);
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Email"] = email,
                ["Token"] = resetToken,
                ["Password"] = newPassword,
                ["ConfirmPassword"] = newPassword
            });

        return await client.PostAsync("/Account/ResetPassword", content);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        string url)
    {
        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        var tokenMatch = Regex.Match(
            content,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(
            tokenMatch.Success,
            $"Antiforgery form alanı bulunamadı ({url}).");

        return WebUtility.HtmlDecode(tokenMatch.Groups[1].Value);
    }

    private static ApplicationDbContext CreateRawContext()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            ConnectionStringEnvironmentVariable)!;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class SqlServerIntegrationFactAttribute : FactAttribute
    {
        public SqlServerIntegrationFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable(
                        ConnectionStringEnvironmentVariable)))
            {
                Skip =
                    $"{ConnectionStringEnvironmentVariable} ayarlanmadığı için " +
                    "geçici SQL Server parola sıfırlama entegrasyon testi atlandı.";
            }
        }
    }
}
