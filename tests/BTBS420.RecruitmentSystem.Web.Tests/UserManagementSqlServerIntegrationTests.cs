using System.Net;
using System.Text.RegularExpressions;
using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Controllers;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class UserManagementSqlServerIntegrationTests :
    IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN38_TEST_SQLSERVER_CONNECTION_STRING";

    private const string TestPassword = "Kan38-Gecici-Sifre-1!";

    private readonly TestWebApplicationFactory _baseFactory;

    public UserManagementSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task Create_GecerliBilgilerleKullaniciOlusturur()
    {
        using var factory = CreateSqlFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan38-Departman-{runId}");
        var userName = $"kan38-olustur-{runId}";
        var email = $"{userName}@example.test";
        using var client = CreateClient(factory);

        var response = await PostCreateAsync(
            client, userName, email, SystemRoles.HiringManager, departmentId);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var user = await context.Users.SingleOrDefaultAsync(u => u.UserName == userName);
        Assert.NotNull(user);
        Assert.Equal(departmentId, user.DepartmentId);
        Assert.True(user.IsActive);

        using var scope = factory.Services.CreateScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = await userManager.GetRolesAsync(user);
        Assert.Equal([SystemRoles.HiringManager], roles);

        var detailsResponse = await client.GetAsync(response.Headers.Location!.OriginalString);
        var detailsBody = await detailsResponse.Content.ReadAsStringAsync();
        Assert.Contains("Geçici şifre", detailsBody);
    }

    [SqlServerIntegrationFact]
    public async Task Create_ActivityLogGeciciSifreyiIcermez()
    {
        using var factory = CreateSqlFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan38-Audit-Departman-{runId}");
        var userName = $"kan38-audit-{runId}";
        var email = $"{userName}@example.test";
        using var client = CreateClient(factory);

        var response = await PostCreateAsync(
            client, userName, email, SystemRoles.Admin, departmentId);
        var detailsResponse = await client.GetAsync(response.Headers.Location!.OriginalString);
        var detailsBody = await detailsResponse.Content.ReadAsStringAsync();

        var tokenMatch = Regex.Match(detailsBody, "<code>([^<]+)</code>");
        Assert.True(tokenMatch.Success, "Geçici şifre gösterimi bulunamadı.");
        var temporaryPassword = tokenMatch.Groups[1].Value;

        await using var context = CreateRawContext();
        var user = await context.Users.SingleAsync(u => u.UserName == userName);
        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityCreated &&
                    l.TargetEntityType == ActivityEntityTypes.User &&
                    l.TargetEntityId == user.Id)
            .FirstOrDefaultAsync();

        Assert.NotNull(log);
        Assert.DoesNotContain(temporaryPassword, log.Summary);
    }

    [SqlServerIntegrationFact]
    public async Task Create_GecersizRolReddedilir()
    {
        using var factory = CreateSqlFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(
            factory, $"Kan38-GecersizRol-Departman-{runId}");
        var userName = $"kan38-gecersizrol-{runId}";
        using var client = CreateClient(factory);

        var response = await PostCreateAsync(
            client, userName, $"{userName}@example.test", SystemRoles.Candidate, departmentId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = CreateRawContext();
        var user = await context.Users.SingleOrDefaultAsync(u => u.UserName == userName);
        Assert.Null(user);
    }

    [SqlServerIntegrationFact]
    public async Task Create_PasifDepartmanaAtamaReddedilir()
    {
        using var factory = CreateSqlFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(
            factory, $"Kan38-PasifDep-Departman-{runId}");
        await PostDeactivateDepartmentAsync(CreateClient(factory), departmentId);
        var userName = $"kan38-pasifdep-{runId}";
        using var client = CreateClient(factory);

        var response = await PostCreateAsync(
            client, userName, $"{userName}@example.test", SystemRoles.HiringManager, departmentId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = CreateRawContext();
        var user = await context.Users.SingleOrDefaultAsync(u => u.UserName == userName);
        Assert.Null(user);
    }

    [SqlServerIntegrationFact]
    public async Task Create_AyniKullaniciAdiIkinciKezReddedilir()
    {
        using var factory = CreateSqlFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(
            factory, $"Kan38-Duplicate-Departman-{runId}");
        var userName = $"kan38-duplicate-{runId}";
        using var firstClient = CreateClient(factory);
        using var secondClient = CreateClient(factory);

        var firstResponse = await PostCreateAsync(
            firstClient,
            userName,
            $"{userName}@example.test",
            SystemRoles.HiringManager,
            departmentId);
        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);

        var secondResponse = await PostCreateAsync(
            secondClient,
            userName,
            $"{userName}-2@example.test",
            SystemRoles.Admin,
            departmentId);
        var body = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Contains("zaten kullan", body);

        await using var context = CreateRawContext();
        var userCount = await context.Users.CountAsync(u => u.UserName == userName);
        Assert.Equal(1, userCount);
    }

    [SqlServerIntegrationFact]
    public async Task Edit_BilgileriGunceller()
    {
        using var factory = CreateSqlFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var originalDepartmentId = await CreateDepartmentAsync(
            factory, $"Kan38-Edit-EskiDep-{runId}");
        var newDepartmentId = await CreateDepartmentAsync(
            factory, $"Kan38-Edit-YeniDep-{runId}");
        var userName = $"kan38-edit-{runId}";
        var userId = await CreateInternalUserAsync(
            factory, userName, $"{userName}@example.test", SystemRoles.HiringManager,
            originalDepartmentId);

        var newUserName = $"kan38-edit-yeni-{runId}";
        var newEmail = $"{newUserName}@example.test";
        using var client = CreateClient(factory);
        var response = await PostEditAsync(
            client, userId, newUserName, newEmail, SystemRoles.RecruitmentSpecialist,
            newDepartmentId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var user = await context.Users.SingleAsync(u => u.Id == userId);
        Assert.Equal(newUserName, user.UserName);
        Assert.Equal(newEmail, user.Email);
        Assert.Equal(newDepartmentId, user.DepartmentId);

        using var scope = factory.Services.CreateScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = await userManager.GetRolesAsync(user);
        Assert.Equal([SystemRoles.RecruitmentSpecialist], roles);

        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityUpdated &&
                    l.TargetEntityType == ActivityEntityTypes.User &&
                    l.TargetEntityId == userId)
            .FirstOrDefaultAsync();
        Assert.NotNull(log);
    }

    [SqlServerIntegrationFact]
    public async Task Edit_BaskaKullaniciAdinaCakisirsaReddedilirVeDegisiklikYapilmaz()
    {
        using var factory = CreateSqlFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(
            factory, $"Kan38-EditCakisma-Departman-{runId}");
        var firstUserName = $"kan38-editcakisma-bir-{runId}";
        var secondUserName = $"kan38-editcakisma-iki-{runId}";
        await CreateInternalUserAsync(
            factory, firstUserName, $"{firstUserName}@example.test", SystemRoles.Admin,
            departmentId);
        var secondUserId = await CreateInternalUserAsync(
            factory, secondUserName, $"{secondUserName}@example.test", SystemRoles.Admin,
            departmentId);

        using var client = CreateClient(factory);
        var response = await PostEditAsync(
            client, secondUserId, firstUserName, $"{secondUserName}@example.test",
            SystemRoles.Admin, departmentId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("zaten kullan", body);

        await using var context = CreateRawContext();
        var secondUser = await context.Users.SingleAsync(u => u.Id == secondUserId);
        Assert.Equal(secondUserName, secondUser.UserName);
    }

    [SqlServerIntegrationFact]
    public async Task Edit_OlmayanKullanici_NotFoundDoner()
    {
        using var factory = CreateSqlFactory();
        var departmentId = await CreateDepartmentAsync(
            factory, $"Kan38-EditYok-Departman-{Guid.NewGuid():N}");

        using var client = CreateClient(factory);
        var response = await PostEditAsync(
            client,
            "kan38-olmayan-kullanici",
            "kan38-olmayan-kullanici",
            "kan38-olmayan-kullanici@example.test",
            SystemRoles.Admin,
            departmentId);

        // NotFound(404), UseStatusCodePagesWithReExecute tarafından POST metodu
        // korunarak /Error/404'e yönlendirilir; ErrorController o rotayı yalnızca
        // [HttpGet] olarak tanımladığından sonuç 405 olarak gözlemlenir
        // (KAN-31'den beri bilinen ve tüm POST/NotFound testlerinde kabul edilen davranış).
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
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
        string email,
        string role,
        int departmentId)
    {
        using var scope = factory.Services.CreateScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
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

    private static async Task PostDeactivateDepartmentAsync(HttpClient client, int departmentId)
    {
        using (client)
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
    }

    private static async Task<HttpResponseMessage> PostCreateAsync(
        HttpClient client,
        string userName,
        string email,
        string role,
        int departmentId)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/Users/Create");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Users/Create");
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["UserName"] = userName,
                ["Email"] = email,
                ["Role"] = role,
                ["DepartmentId"] = departmentId.ToString()
            });

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
        // Antiforgery token, hedef kullanıcının Edit sayfası mevcut olmayabileceğinden
        // (ör. NotFound senaryosu) her zaman var olan Create sayfasından alınır.
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
                    "geçici SQL Server kullanıcı yönetimi entegrasyon testi atlandı.";
            }
        }
    }
}
