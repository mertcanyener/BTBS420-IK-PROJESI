using System.Net;
using System.Text.RegularExpressions;
using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class UserActivityHistorySqlServerIntegrationTests :
    IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN40_TEST_SQLSERVER_CONNECTION_STRING";

    private const string TestPassword = "Kan40-Gecici-Sifre-1!";

    private readonly TestWebApplicationFactory _baseFactory;

    public UserActivityHistorySqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task Details_SadeceHedefKullaniciyaAitOlaylarGorunurBaskasinaSizmaz()
    {
        using var factory = CreateSqlFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan40-Izolasyon-Dep-{runId}");
        var userAId = await CreateInternalUserAsync(
            factory, $"kan40-izolasyon-a-{runId}", SystemRoles.HiringManager, departmentId);
        var userBId = await CreateInternalUserAsync(
            factory, $"kan40-izolasyon-b-{runId}", SystemRoles.HiringManager, departmentId);

        using var client = CreateClient(factory);
        var deactivateResponse = await PostDeactivateAsync(client, userAId);
        Assert.Equal(HttpStatusCode.Redirect, deactivateResponse.StatusCode);

        var detailsAResponse = await client.GetAsync($"/Users/Details/{userAId}");
        var detailsABody = await detailsAResponse.Content.ReadAsStringAsync();
        Assert.Equal(2, Regex.Matches(detailsABody, "<tr>").Count);

        var detailsBResponse = await client.GetAsync($"/Users/Details/{userBId}");
        var detailsBBody = await detailsBResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<tr>", detailsBBody);
    }

    [SqlServerIntegrationFact]
    public async Task Details_RolDegisikligiEskiVeYeniDegerleGosterilir()
    {
        using var factory = CreateSqlFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan40-RolGecmisi-Dep-{runId}");
        var userName = $"kan40-rolgecmisi-{runId}";
        var userId = await CreateInternalUserAsync(
            factory, userName, SystemRoles.RecruitmentSpecialist, departmentId);

        using var client = CreateClient(factory);
        var response = await PostEditAsync(
            client, userId, userName, $"{userName}@example.test", SystemRoles.Admin,
            departmentId);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityUpdated &&
                    l.TargetEntityType == ActivityEntityTypes.User &&
                    l.TargetEntityId == userId)
            .FirstOrDefaultAsync();

        Assert.NotNull(log);
        Assert.Contains("Rol:", log.Summary);
        Assert.Contains("-> Admin", log.Summary);
    }

    [SqlServerIntegrationFact]
    public async Task Details_LoginLogoutOlaylariZamanCizelgesindeGorunmez()
    {
        using var factory = CreateSqlFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan40-AuthHaric-Dep-{runId}");
        var userId = await CreateInternalUserAsync(
            factory, $"kan40-authharic-{runId}", SystemRoles.HiringManager, departmentId);

        using (var scope = factory.Services.CreateScope())
        {
            var activityLogService =
                scope.ServiceProvider.GetRequiredService<IActivityLogService>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            activityLogService.Stage(
                new ActivityLogEntry(
                    ActivityActionCodes.AuthenticationSucceeded,
                    "Kullanıcı başarıyla giriş yaptı.",
                    ActivityEntityTypes.User,
                    userId));
            await context.SaveChangesAsync();
        }

        using var client = CreateClient(factory);
        var response = await client.GetAsync($"/Users/Details/{userId}");
        var body = await response.Content.ReadAsStringAsync();

        // Kullanıcı oluşturma (Create) hiçbir zaman ActivityLog'a yazılmadığından
        // (CreateInternalUserAsync doğrudan UserManager kullanır) bu kullanıcının
        // yönetimsel işlem geçmişi boştur; eklenen login olayı listede görünmemeli.
        Assert.DoesNotContain("<tr>", body);
    }

    private WebApplicationFactory<Program> CreateSqlFactory()
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
        });
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true
            });
        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);

        return client;
    }

    private static async Task EnsureRolesSeededAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var roleManager =
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var roleName in SystemRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }
    }

    private static async Task<string> CreateInternalUserAsync(
        WebApplicationFactory<Program> factory,
        string userName,
        string role,
        int departmentId)
    {
        using var scope = factory.Services.CreateScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = $"{userName}@example.test",
            EmailConfirmed = true,
            IsActive = true,
            DepartmentId = departmentId
        };

        var createResult = await userManager.CreateAsync(user, TestPassword);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", createResult.Errors.Select(error => error.Description)));
        }

        await userManager.AddToRoleAsync(user, role);

        return user.Id;
    }

    private async Task<int> CreateDepartmentAsync(WebApplicationFactory<Program> factory, string name)
    {
        using var client = CreateClient(factory);
        var token = await GetAntiforgeryTokenAsync(client, "/Departments/Create");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Departments/Create");
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Name"] = name
            });

        var response = await client.SendAsync(request);
        if (response.StatusCode != HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException(
                $"Departman oluşturulamadı: {response.StatusCode}");
        }

        await using var context = CreateRawContext();
        var department = await context.Departments.SingleAsync(d => d.Name == name);

        return department.Id;
    }

    private static async Task<HttpResponseMessage> PostDeactivateAsync(
        HttpClient client,
        string id)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/Users/Create");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Users/Deactivate/{id}");
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token });

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PostEditAsync(
        HttpClient client,
        string id,
        string userName,
        string email,
        string role,
        int departmentId)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/Users/Create");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Users/Edit/{id}");
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = id,
                ["UserName"] = userName,
                ["Email"] = email,
                ["Role"] = role,
                ["DepartmentId"] = departmentId.ToString()
            });

        return await client.SendAsync(request);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
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

    private ApplicationDbContext CreateRawContext()
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
                    "geçici SQL Server kullanıcı geçmişi entegrasyon testi atlandı.";
            }
        }
    }
}
