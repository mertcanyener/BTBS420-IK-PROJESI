using System.Net;
using System.Text.RegularExpressions;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class UserSqlServerIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN37_TEST_SQLSERVER_CONNECTION_STRING";

    private const string TestPassword = "Kan37-Gecici-Sifre-1!";

    private readonly TestWebApplicationFactory _baseFactory;

    public UserSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task Index_AramaFiltresi_KullaniciAdiVeyaEmaileGoreEslesir()
    {
        using var factory = CreateSqlFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var matchingUserName = $"kan37-arama-{runId}";
        var otherUserName = $"kan37-digerkullanici-{runId}";
        await CreateUserAsync(factory, matchingUserName, $"{matchingUserName}@example.test");
        await CreateUserAsync(factory, otherUserName, $"{otherUserName}@example.test");

        using var client = CreateClient(factory);
        var response = await client.GetAsync($"/Users?search={matchingUserName}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(matchingUserName, body);
        Assert.DoesNotContain(otherUserName, body);
    }

    [SqlServerIntegrationFact]
    public async Task Index_RolFiltresi_SadeceORoldekiKullanicilariGetirir()
    {
        using var factory = CreateSqlFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var adminUserName = $"kan37-adminrol-{runId}";
        var candidateUserName = $"kan37-adayrol-{runId}";
        await CreateUserAsync(
            factory, adminUserName, $"{adminUserName}@example.test", roles: [SystemRoles.Admin]);
        await CreateUserAsync(
            factory, candidateUserName, $"{candidateUserName}@example.test",
            roles: [SystemRoles.Candidate]);

        using var client = CreateClient(factory);
        var response = await client.GetAsync($"/Users?role={SystemRoles.Admin}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(adminUserName, body);
        Assert.DoesNotContain(candidateUserName, body);
    }

    [SqlServerIntegrationFact]
    public async Task Index_DepartmanFiltresi_SadeceODepartmandakiKullanicilariGetirir()
    {
        using var factory = CreateSqlFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var firstDepartmentId = await CreateDepartmentAsync(
            factory, $"Kan37-Departman-Bir-{runId}");
        var secondDepartmentId = await CreateDepartmentAsync(
            factory, $"Kan37-Departman-Iki-{runId}");
        var firstUserName = $"kan37-dep-bir-{runId}";
        var secondUserName = $"kan37-dep-iki-{runId}";
        await CreateUserAsync(
            factory, firstUserName, $"{firstUserName}@example.test",
            departmentId: firstDepartmentId);
        await CreateUserAsync(
            factory, secondUserName, $"{secondUserName}@example.test",
            departmentId: secondDepartmentId);

        using var client = CreateClient(factory);
        var response = await client.GetAsync($"/Users?departmentId={firstDepartmentId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(firstUserName, body);
        Assert.DoesNotContain(secondUserName, body);
    }

    [SqlServerIntegrationFact]
    public async Task Index_DurumFiltresi_SadeceAktifVeyaPasifKullanicilariGetirir()
    {
        using var factory = CreateSqlFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var activeUserName = $"kan37-aktif-{runId}";
        var inactiveUserName = $"kan37-pasif-{runId}";
        await CreateUserAsync(
            factory, activeUserName, $"{activeUserName}@example.test", isActive: true);
        await CreateUserAsync(
            factory, inactiveUserName, $"{inactiveUserName}@example.test", isActive: false);

        using var client = CreateClient(factory);
        var response = await client.GetAsync("/Users?isActive=false");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(inactiveUserName, body);
        Assert.DoesNotContain(activeUserName, body);
    }

    [SqlServerIntegrationFact]
    public async Task Index_TumFiltrelerBirlikteAndMantigiylaCalisir()
    {
        using var factory = CreateSqlFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan37-And-Departman-{runId}");
        var otherDepartmentId = await CreateDepartmentAsync(
            factory, $"Kan37-And-DigerDepartman-{runId}");

        var matchingUserName = $"kan37-and-eslesen-{runId}";
        await CreateUserAsync(
            factory, matchingUserName, $"{matchingUserName}@example.test",
            roles: [SystemRoles.HiringManager], departmentId: departmentId, isActive: true);

        var wrongRoleUserName = $"kan37-and-yanlisrol-{runId}";
        await CreateUserAsync(
            factory, wrongRoleUserName, $"{wrongRoleUserName}@example.test",
            roles: [SystemRoles.Candidate], departmentId: departmentId, isActive: true);

        var wrongDepartmentUserName = $"kan37-and-yanlisdep-{runId}";
        await CreateUserAsync(
            factory, wrongDepartmentUserName, $"{wrongDepartmentUserName}@example.test",
            roles: [SystemRoles.HiringManager], departmentId: otherDepartmentId, isActive: true);

        var wrongStatusUserName = $"kan37-and-yanlisdurum-{runId}";
        await CreateUserAsync(
            factory, wrongStatusUserName, $"{wrongStatusUserName}@example.test",
            roles: [SystemRoles.HiringManager], departmentId: departmentId, isActive: false);

        using var client = CreateClient(factory);
        var response = await client.GetAsync(
            $"/Users?role={Uri.EscapeDataString(SystemRoles.HiringManager)}" +
            $"&departmentId={departmentId}&isActive=true");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(matchingUserName, body);
        Assert.DoesNotContain(wrongRoleUserName, body);
        Assert.DoesNotContain(wrongDepartmentUserName, body);
        Assert.DoesNotContain(wrongStatusUserName, body);
    }

    [SqlServerIntegrationFact]
    public async Task Index_DepartmanDropdownu_PasifDepartmanlariDaIcerir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentName = $"Kan37-Pasif-Dropdown-{runId}";
        var departmentId = await CreateDepartmentAsync(factory, departmentName);
        using var deactivateClient = CreateClient(factory);
        await PostDeactivateDepartmentAsync(deactivateClient, departmentId);

        using var client = CreateClient(factory);
        var response = await client.GetAsync("/Users");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(departmentName, body);
    }

    [SqlServerIntegrationFact]
    public async Task Details_KullaniciRolVeDurumDogruGosterir()
    {
        using var factory = CreateSqlFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var departmentName = $"Kan37-Detay-Departman-{runId}";
        var departmentId = await CreateDepartmentAsync(factory, departmentName);
        var userName = $"kan37-detay-{runId}";
        var userId = await CreateUserAsync(
            factory, userName, $"{userName}@example.test",
            roles: [SystemRoles.RecruitmentSpecialist], departmentId: departmentId,
            isActive: false);

        using var client = CreateClient(factory);
        var response = await client.GetAsync($"/Users/Details/{userId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(userName, body);
        Assert.Contains("Uzman", body);
        Assert.Contains(departmentName, body);
        Assert.Contains("Pasif", body);
    }

    [SqlServerIntegrationFact]
    public async Task Details_OlmayanKullanici_NotFoundDoner()
    {
        using var factory = CreateSqlFactory();

        using var client = CreateClient(factory);
        var response = await client.GetAsync("/Users/Details/kan37-olmayan-kullanici");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

    private static async Task<string> CreateUserAsync(
        WebApplicationFactory<Program> factory,
        string userName,
        string email,
        IReadOnlyList<string>? roles = null,
        int? departmentId = null,
        bool isActive = true)
    {
        using var scope = factory.Services.CreateScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            IsActive = isActive,
            DepartmentId = departmentId
        };

        var createResult = await userManager.CreateAsync(user, TestPassword);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", createResult.Errors.Select(error => error.Description)));
        }

        if (roles is { Count: > 0 })
        {
            await userManager.AddToRolesAsync(user, roles);
        }

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

    private static async Task PostDeactivateDepartmentAsync(HttpClient client, int departmentId)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/Departments");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/Departments/Deactivate/{departmentId}");
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            });

        var response = await client.SendAsync(request);
        if (response.StatusCode != HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException(
                $"Departman pasif yapılamadı: {response.StatusCode}");
        }
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
                    "geçici SQL Server kullanıcı entegrasyon testi atlandı.";
            }
        }
    }
}
