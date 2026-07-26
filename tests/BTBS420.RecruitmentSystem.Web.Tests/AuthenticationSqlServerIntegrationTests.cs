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

public sealed class AuthenticationSqlServerIntegrationTests :
    IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN31_TEST_SQLSERVER_CONNECTION_STRING";

    private const string TestPassword = "Kan31-Gecici-Sifre-1!";

    private readonly TestWebApplicationFactory _baseFactory;

    public AuthenticationSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task Login_KullaniciAdiIleDogruParolaBasarili()
    {
        using var factory = CreateRealAuthenticationFactory();
        var runId = Guid.NewGuid().ToString("N");
        var (userName, _, _) = await CreateUserAsync(factory, runId);
        using var client = CreateClient(factory);

        var response = await PostLoginAsync(client, userName, TestPassword);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out _));
    }

    [SqlServerIntegrationFact]
    public async Task Login_EmailIleDogruParolaBasarili()
    {
        using var factory = CreateRealAuthenticationFactory();
        var runId = Guid.NewGuid().ToString("N");
        var (_, email, _) = await CreateUserAsync(factory, runId);
        using var client = CreateClient(factory);

        var response = await PostLoginAsync(client, email, TestPassword);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out _));
    }

    [SqlServerIntegrationFact]
    public async Task Login_AktifKullaniciGirisYapabilir()
    {
        using var factory = CreateRealAuthenticationFactory();
        var runId = Guid.NewGuid().ToString("N");
        var (userName, _, _) = await CreateUserAsync(factory, runId, isActive: true);
        using var client = CreateClient(factory);

        var response = await PostLoginAsync(client, userName, TestPassword);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task Login_YanlisParolaBasarisiz()
    {
        using var factory = CreateRealAuthenticationFactory();
        var runId = Guid.NewGuid().ToString("N");
        var (userName, _, _) = await CreateUserAsync(factory, runId);
        using var client = CreateClient(factory);

        var response = await PostLoginAsync(client, userName, "Yanlis-Parola-1!");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "e-posta veya parola",
            body);
    }

    [SqlServerIntegrationFact]
    public async Task Login_OlmayanKullaniciBasarisiz()
    {
        using var factory = CreateRealAuthenticationFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var client = CreateClient(factory);

        var response = await PostLoginAsync(
            client,
            $"olmayan-kullanici-{runId}",
            "Herhangi-Bir-Parola-1!");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "e-posta veya parola",
            body);
    }

    [SqlServerIntegrationFact]
    public async Task Login_IkiBasarisizDurumdaAyniMesajiGosterir()
    {
        using var factory = CreateRealAuthenticationFactory();
        var runId = Guid.NewGuid().ToString("N");
        var (userName, _, _) = await CreateUserAsync(factory, runId);
        using var wrongPasswordClient = CreateClient(factory);
        using var unknownUserClient = CreateClient(factory);

        var wrongPasswordResponse = await PostLoginAsync(
            wrongPasswordClient,
            userName,
            "Yanlis-Parola-1!");
        var unknownUserResponse = await PostLoginAsync(
            unknownUserClient,
            $"olmayan-kullanici-{runId}",
            "Herhangi-Bir-Parola-1!");

        var wrongPasswordBody =
            await wrongPasswordResponse.Content.ReadAsStringAsync();
        var unknownUserBody =
            await unknownUserResponse.Content.ReadAsStringAsync();
        var wrongPasswordMessage = ExtractValidationSummary(wrongPasswordBody);
        var unknownUserMessage = ExtractValidationSummary(unknownUserBody);

        Assert.Equal(wrongPasswordMessage, unknownUserMessage);
        Assert.DoesNotContain(userName, wrongPasswordMessage);
        Assert.DoesNotContain(userName, unknownUserMessage);
    }

    [SqlServerIntegrationFact]
    public async Task Login_PasifKullaniciDogruParolaIleGirisYapamaz()
    {
        using var factory = CreateRealAuthenticationFactory();
        var runId = Guid.NewGuid().ToString("N");
        var (userName, _, _) = await CreateUserAsync(factory, runId, isActive: false);
        using var client = CreateClient(factory);

        var response = await PostLoginAsync(client, userName, TestPassword);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "e-posta veya parola",
            body);
    }

    [SqlServerIntegrationFact]
    public async Task Session_PasifeAlinanKullanicininMevcutOturumuSonrakiIstekteReddedilir()
    {
        using var factory = CreateRealAuthenticationFactory();
        var runId = Guid.NewGuid().ToString("N");
        var (userName, _, _) = await CreateUserAsync(factory, runId);
        using var client = CreateClient(factory);
        var loginResponse = await PostLoginAsync(client, userName, TestPassword);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        var beforeDeactivation = await client.GetAsync("/Notifications");
        Assert.Equal(HttpStatusCode.OK, beforeDeactivation.StatusCode);

        await SetActiveAsync(factory, userName, isActive: false);

        var afterDeactivation = await client.GetAsync("/Notifications");

        Assert.Equal(HttpStatusCode.Redirect, afterDeactivation.StatusCode);
        Assert.Contains(
            "/Account/Login",
            afterDeactivation.Headers.Location?.OriginalString ?? string.Empty);
    }

    [SqlServerIntegrationFact]
    public async Task Logout_OturumuSonlandirir()
    {
        using var factory = CreateRealAuthenticationFactory();
        var runId = Guid.NewGuid().ToString("N");
        var (userName, _, _) = await CreateUserAsync(factory, runId);
        using var client = CreateClient(factory);
        await PostLoginAsync(client, userName, TestPassword);

        var logoutResponse = await PostLogoutAsync(client);
        Assert.Equal(HttpStatusCode.Redirect, logoutResponse.StatusCode);

        var afterLogout = await client.GetAsync("/Notifications");

        Assert.Equal(HttpStatusCode.Redirect, afterLogout.StatusCode);
        Assert.Contains(
            "/Account/Login",
            afterLogout.Headers.Location?.OriginalString ?? string.Empty);
    }

    [SqlServerIntegrationFact]
    public async Task Logout_YalnizPostIleCalisir()
    {
        using var factory = CreateRealAuthenticationFactory();
        var runId = Guid.NewGuid().ToString("N");
        var (userName, _, _) = await CreateUserAsync(factory, runId);
        using var client = CreateClient(factory);
        await PostLoginAsync(client, userName, TestPassword);

        var getResponse = await client.GetAsync("/Account/Logout");

        Assert.NotEqual(HttpStatusCode.OK, getResponse.StatusCode);

        var stillProtected = await client.GetAsync("/Notifications");
        Assert.Equal(HttpStatusCode.OK, stillProtected.StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task Logout_AntiforgeryTokenOlmadanReddeder()
    {
        using var factory = CreateRealAuthenticationFactory();
        var runId = Guid.NewGuid().ToString("N");
        var (userName, _, _) = await CreateUserAsync(factory, runId);
        using var client = CreateClient(factory);
        await PostLoginAsync(client, userName, TestPassword);
        using var content = new FormUrlEncodedContent([]);

        var response = await client.PostAsync("/Account/Logout", content);

        Assert.NotEqual(HttpStatusCode.Redirect, response.StatusCode);

        var stillProtected = await client.GetAsync("/Notifications");
        Assert.Equal(HttpStatusCode.OK, stillProtected.StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task AnonimKullanici_KorumaliSayfayaErisemez()
    {
        using var factory = CreateRealAuthenticationFactory();
        using var client = CreateClient(factory);

        var response = await client.GetAsync("/Notifications");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "/Account/Login",
            response.Headers.Location?.OriginalString ?? string.Empty);
    }

    [SqlServerIntegrationFact]
    public async Task Login_LocalReturnUrlCalisir()
    {
        using var factory = CreateRealAuthenticationFactory();
        var runId = Guid.NewGuid().ToString("N");
        var (userName, _, _) = await CreateUserAsync(factory, runId);
        using var client = CreateClient(factory);

        var response = await PostLoginAsync(
            client,
            userName,
            TestPassword,
            returnUrl: "/Notifications");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "/Notifications",
            response.Headers.Location?.OriginalString);
    }

    [SqlServerIntegrationFact]
    public async Task Login_HariciReturnUrlReddedilir()
    {
        using var factory = CreateRealAuthenticationFactory();
        var runId = Guid.NewGuid().ToString("N");
        var (userName, _, _) = await CreateUserAsync(factory, runId);
        using var client = CreateClient(factory);

        var response = await PostLoginAsync(
            client,
            userName,
            TestPassword,
            returnUrl: "https://evil.example/steal");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [SqlServerIntegrationFact]
    public async Task Login_BasarisizDenemedeParolaResponsetaGorunmez()
    {
        using var factory = CreateRealAuthenticationFactory();
        var runId = Guid.NewGuid().ToString("N");
        var (userName, _, _) = await CreateUserAsync(factory, runId);
        using var client = CreateClient(factory);
        const string wrongPassword = "Gizli-Yanlis-Parola-1!";

        var response = await PostLoginAsync(client, userName, wrongPassword);
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(wrongPassword, body);
        Assert.DoesNotContain(TestPassword, body);
    }

    [SqlServerIntegrationFact]
    public async Task ActivityLog_KimlikDogrulamaOlaylariGuvenliSekildeKaydedilir()
    {
        using var factory = CreateRealAuthenticationFactory();
        var runId = Guid.NewGuid().ToString("N");
        var (userName, _, _) = await CreateUserAsync(factory, runId);
        using var client = CreateClient(factory);

        await PostLoginAsync(client, userName, "Yanlis-Parola-1!");
        var loginResponse = await PostLoginAsync(client, userName, TestPassword);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        var userId = await GetUserIdAsync(factory, userName);

        await SetActiveAsync(factory, userName, isActive: false);
        await client.GetAsync("/Notifications");

        await using var context = CreateRawContext();
        var logs = await context.ActivityLogs
            .Where(log => log.ActionCode == ActivityActionCodes.AuthenticationFailed)
            .ToListAsync();

        Assert.NotEmpty(logs);
        Assert.All(
            logs,
            log =>
            {
                Assert.Null(log.TargetEntityId);
                Assert.Null(log.TargetEntityType);
                Assert.DoesNotContain(userName, log.Summary);
                Assert.DoesNotContain(userId, log.Summary);
                Assert.DoesNotContain(TestPassword, log.Summary);
            });

        var succeededLog = await context.ActivityLogs
            .Where(
                log =>
                    log.ActionCode == ActivityActionCodes.AuthenticationSucceeded &&
                    log.TargetEntityId == userId)
            .FirstOrDefaultAsync();
        Assert.NotNull(succeededLog);
        Assert.Equal(ActivityEntityTypes.User, succeededLog.TargetEntityType);
    }

    [SqlServerIntegrationFact]
    public async Task ActivityLog_LogoutOlayiGercekKullaniciIdIleKaydedilir()
    {
        using var factory = CreateRealAuthenticationFactory();
        var runId = Guid.NewGuid().ToString("N");
        var (userName, _, _) = await CreateUserAsync(factory, runId);
        var userId = await GetUserIdAsync(factory, userName);
        using var client = CreateClient(factory);

        await PostLoginAsync(client, userName, TestPassword);
        await PostLogoutAsync(client);

        await using var context = CreateRawContext();
        var signedOutLog = await context.ActivityLogs
            .Where(
                log =>
                    log.ActionCode == ActivityActionCodes.AuthenticationSignedOut &&
                    log.TargetEntityId == userId)
            .FirstOrDefaultAsync();

        Assert.NotNull(signedOutLog);
        Assert.Equal(ActivityEntityTypes.User, signedOutLog.TargetEntityType);
    }

    private WebApplicationFactory<Program> CreateRealAuthenticationFactory()
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

    private static async Task<(string UserName, string Email, string Password)>
        CreateUserAsync(
            WebApplicationFactory<Program> factory,
            string runId,
            bool isActive = true)
    {
        using var scope = factory.Services.CreateScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var userName = $"kan31-user-{runId}";
        var email = $"kan31-{runId}@example.test";

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            IsActive = isActive
        };

        var result = await userManager.CreateAsync(user, TestPassword);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Test kullanıcısı oluşturulamadı: " +
                string.Join(", ", result.Errors.Select(error => error.Code)));
        }

        return (userName, email, TestPassword);
    }

    private static async Task SetActiveAsync(
        WebApplicationFactory<Program> factory,
        string userName,
        bool isActive)
    {
        using var scope = factory.Services.CreateScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(userName)
            ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

        user.IsActive = isActive;
        await userManager.UpdateAsync(user);
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

    private static async Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client,
        string usernameOrEmail,
        string password,
        string? returnUrl = null)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["UsernameOrEmail"] = usernameOrEmail,
                ["Password"] = password,
                ["ReturnUrl"] = returnUrl ?? string.Empty
            });

        return await client.PostAsync("/Account/Login", content);
    }

    private static async Task<HttpResponseMessage> PostLogoutAsync(HttpClient client)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/");
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            });

        return await client.PostAsync("/Account/Logout", content);
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

    private static string ExtractValidationSummary(string html)
    {
        var match = Regex.Match(
            html,
            "<div[^>]*class=\"[^\"]*alert-danger[^\"]*\"[^>]*>(.*?)</div>",
            RegexOptions.Singleline);
        Assert.True(match.Success, "Doğrulama özeti bulunamadı.");

        return match.Groups[1].Value.Trim();
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
                    "geçici SQL Server kimlik doğrulama entegrasyon testi atlandı.";
            }
        }
    }
}
