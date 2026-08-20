using System.Net;
using System.Text.RegularExpressions;
using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class UserLifecycleSqlServerIntegrationTests :
    IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN39_TEST_SQLSERVER_CONNECTION_STRING";

    private const string TestPassword = "Kan39-Gecici-Sifre-1!";

    private readonly TestWebApplicationFactory _baseFactory;

    public UserLifecycleSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task Deactivate_IkinciAdminVarkenBasarili()
    {
        using var factory = CreateSqlFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan39-Deactivate-Dep-{runId}");
        await CreateInternalUserAsync(
            factory, $"kan39-admin-bir-{runId}", SystemRoles.Admin, departmentId);
        var targetUserId = await CreateInternalUserAsync(
            factory, $"kan39-admin-iki-{runId}", SystemRoles.Admin, departmentId);

        using var client = CreateClient(factory);
        var response = await PostDeactivateAsync(client, targetUserId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var targetUser = await context.Users.SingleAsync(u => u.Id == targetUserId);
        Assert.False(targetUser.IsActive);

        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityStatusChanged &&
                    l.TargetEntityType == ActivityEntityTypes.User &&
                    l.TargetEntityId == targetUserId)
            .FirstOrDefaultAsync();
        Assert.NotNull(log);
    }

    [SqlServerIntegrationFact]
    public async Task Deactivate_SonAktifAdminEngellenir()
    {
        // Paylaşılan BTBS420_KAN39_Test veritabanında diğer testlerin oluşturduğu
        // başka Admin hesapları bulunabileceğinden ("son aktif Admin" sayımı tüm
        // veritabanını kapsar), bu test kendi izole, admin'siz geçici
        // veritabanını kullanır (KAN-32'deki izole veritabanı deseniyle aynı).
        var databaseName = await CreateIsolatedDatabaseAsync();

        try
        {
            var connectionString = BuildConnectionString(databaseName);
            using var factory = CreateSqlFactory(connectionString);
            await EnsureRolesSeededAsync(factory);
            var runId = Guid.NewGuid().ToString("N");
            var departmentId = await CreateDepartmentAsync(
                factory, $"Kan39-SonAdmin-Dep-{runId}", connectionString);
            var onlyAdminId = await CreateInternalUserAsync(
                factory, $"kan39-tek-admin-{runId}", SystemRoles.Admin, departmentId);

            using var client = CreateClient(factory);
            var response = await PostDeactivateAsync(client, onlyAdminId);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var detailsResponse =
                await client.GetAsync(response.Headers.Location!.OriginalString);
            var detailsBody = await detailsResponse.Content.ReadAsStringAsync();
            Assert.Contains("son aktif Admin", detailsBody);

            await using var context = CreateRawContext(connectionString);
            var user = await context.Users.SingleAsync(u => u.Id == onlyAdminId);
            Assert.True(user.IsActive);
        }
        finally
        {
            await DropIsolatedDatabaseAsync(databaseName);
        }
    }

    [SqlServerIntegrationFact]
    public async Task Deactivate_UzmanIcinAdminKisitiUygulanmaz()
    {
        using var factory = CreateSqlFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan39-Uzman-Dep-{runId}");
        var uzmanId = await CreateInternalUserAsync(
            factory, $"kan39-uzman-{runId}", SystemRoles.RecruitmentSpecialist, departmentId);

        using var client = CreateClient(factory);
        var response = await PostDeactivateAsync(client, uzmanId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var user = await context.Users.SingleAsync(u => u.Id == uzmanId);
        Assert.False(user.IsActive);
    }

    [SqlServerIntegrationFact]
    public async Task Activate_PasifIcselKullaniciyiTekrarAktifYapar()
    {
        using var factory = CreateSqlFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan39-Activate-Dep-{runId}");
        var userId = await CreateInternalUserAsync(
            factory, $"kan39-activate-{runId}", SystemRoles.HiringManager, departmentId);

        using var deactivateClient = CreateClient(factory);
        await PostDeactivateAsync(deactivateClient, userId);

        using var activateClient = CreateClient(factory);
        var response = await PostActivateAsync(activateClient, userId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var user = await context.Users.SingleAsync(u => u.Id == userId);
        Assert.True(user.IsActive);
    }

    [SqlServerIntegrationFact]
    public async Task Deactivate_KullaniciFizikselSilinmezSoftDeleteKorunur()
    {
        using var factory = CreateSqlFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan39-SoftDelete-Dep-{runId}");
        await CreateInternalUserAsync(
            factory, $"kan39-softdelete-admin-{runId}", SystemRoles.Admin, departmentId);
        var userId = await CreateInternalUserAsync(
            factory, $"kan39-softdelete-{runId}", SystemRoles.HiringManager, departmentId);

        using var client = CreateClient(factory);
        await PostDeactivateAsync(client, userId);

        await using var context = CreateRawContext();
        var userExists = await context.Users.AnyAsync(u => u.Id == userId);
        Assert.True(userExists);
    }

    [SqlServerIntegrationFact]
    public async Task Edit_SonAktifAdminRoluDegistirilemez()
    {
        // Deactivate_SonAktifAdminEngellenir testindeki gerekçeyle aynı:
        // "son aktif Admin" sayımı tüm veritabanını kapsadığından izole
        // veritabanı kullanılır.
        var databaseName = await CreateIsolatedDatabaseAsync();

        try
        {
            var connectionString = BuildConnectionString(databaseName);
            using var factory = CreateSqlFactory(connectionString);
            await EnsureRolesSeededAsync(factory);
            var runId = Guid.NewGuid().ToString("N");
            var departmentId = await CreateDepartmentAsync(
                factory, $"Kan39-EditSonAdmin-Dep-{runId}", connectionString);
            var userName = $"kan39-editsonadmin-{runId}";
            var onlyAdminId = await CreateInternalUserAsync(
                factory, userName, SystemRoles.Admin, departmentId);

            using var client = CreateClient(factory);
            var response = await PostEditAsync(
                client, onlyAdminId, userName, $"{userName}@example.test",
                SystemRoles.HiringManager, departmentId);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("son aktif Admin", body);

            using var scope = factory.Services.CreateScope();
            var userManager =
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await using var context = CreateRawContext(connectionString);
            var user = await context.Users.SingleAsync(u => u.Id == onlyAdminId);
            Assert.True(await userManager.IsInRoleAsync(user, SystemRoles.Admin));
        }
        finally
        {
            await DropIsolatedDatabaseAsync(databaseName);
        }
    }

    [SqlServerIntegrationFact]
    public async Task Edit_IkinciAdminVarkenRolDegistirilebilir()
    {
        using var factory = CreateSqlFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(
            factory, $"Kan39-EditIkinciAdmin-Dep-{runId}");
        await CreateInternalUserAsync(
            factory, $"kan39-ikinciadmin-bir-{runId}", SystemRoles.Admin, departmentId);
        var userName = $"kan39-ikinciadmin-iki-{runId}";
        var secondAdminId = await CreateInternalUserAsync(
            factory, userName, SystemRoles.Admin, departmentId);

        using var client = CreateClient(factory);
        var response = await PostEditAsync(
            client, secondAdminId, userName, $"{userName}@example.test",
            SystemRoles.HiringManager, departmentId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await using var context = CreateRawContext();
        var user = await context.Users.SingleAsync(u => u.Id == secondAdminId);
        Assert.False(await userManager.IsInRoleAsync(user, SystemRoles.Admin));
        Assert.True(await userManager.IsInRoleAsync(user, SystemRoles.HiringManager));
    }

    private WebApplicationFactory<Program> CreateSqlFactory(string? connectionStringOverride = null)
    {
        var connectionString = connectionStringOverride ?? Environment.GetEnvironmentVariable(
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

    private async Task<int> CreateDepartmentAsync(
        WebApplicationFactory<Program> factory,
        string name,
        string? connectionStringOverride = null)
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

        await using var context = CreateRawContext(connectionStringOverride);
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

    private static async Task<HttpResponseMessage> PostActivateAsync(
        HttpClient client,
        string id)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/Users/Create");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Users/Activate/{id}");
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

    private ApplicationDbContext CreateRawContext(string? connectionStringOverride = null)
    {
        var connectionString = connectionStringOverride ?? Environment.GetEnvironmentVariable(
            ConnectionStringEnvironmentVariable)!;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    private static string BuildConnectionString(string databaseName)
    {
        var baseConnectionString = Environment.GetEnvironmentVariable(
            ConnectionStringEnvironmentVariable)!;
        var builder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = databaseName
        };

        return builder.ConnectionString;
    }

    private static async Task<string> CreateIsolatedDatabaseAsync()
    {
        var databaseName = $"BTBS420_KAN39_LastAdmin_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(BuildConnectionString(databaseName))
            .Options;

        await using var context = new ApplicationDbContext(options);
        await context.Database.MigrateAsync();

        return databaseName;
    }

    private static async Task DropIsolatedDatabaseAsync(string databaseName)
    {
        SqlConnection.ClearAllPools();

        await using var connection = new SqlConnection(BuildConnectionString("master"));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"DROP DATABASE IF EXISTS [{databaseName}];";
        await command.ExecuteNonQueryAsync();
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
                    "geçici SQL Server kullanıcı yaşam döngüsü entegrasyon testi atlandı.";
            }
        }
    }
}
