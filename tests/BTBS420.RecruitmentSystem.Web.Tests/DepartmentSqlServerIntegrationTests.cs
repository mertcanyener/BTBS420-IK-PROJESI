using System.Net;
using System.Text.RegularExpressions;
using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class DepartmentSqlServerIntegrationTests :
    IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN34_TEST_SQLSERVER_CONNECTION_STRING";

    private readonly TestWebApplicationFactory _baseFactory;

    public DepartmentSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task Create_GecerliIsimIleDepartmanOlusturur()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var name = $"Kan34-Departman-{runId}";
        using var client = CreateClient(factory);

        var response = await PostCreateAsync(client, name);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var department = await context.Departments.SingleOrDefaultAsync(d => d.Name == name);
        Assert.NotNull(department);
        Assert.True(department.IsActive);
    }

    [SqlServerIntegrationFact]
    public async Task Create_AyniIsimIkinciKezReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var name = $"Kan34-Duplicate-{runId}";
        using var firstClient = CreateClient(factory);
        using var secondClient = CreateClient(factory);

        var firstResponse = await PostCreateAsync(firstClient, name);
        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);

        var secondResponse = await PostCreateAsync(secondClient, name);
        var body = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Contains("zaten kullan", body);
    }

    [SqlServerIntegrationFact]
    public async Task Create_IsimCasingDuplicateEngellenir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var name = $"Kan34-Casing-{runId}";
        using var firstClient = CreateClient(factory);
        using var secondClient = CreateClient(factory);

        var firstResponse = await PostCreateAsync(firstClient, name);
        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);

        var secondResponse = await PostCreateAsync(secondClient, name.ToUpperInvariant());
        var body = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Contains("zaten kullan", body);
    }

    [SqlServerIntegrationFact]
    public async Task Edit_DepartmanAdiGuncellenir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var originalName = $"Kan34-Edit-Once-{runId}";
        var newName = $"Kan34-Edit-Twice-{runId}";
        using var createClient = CreateClient(factory);
        await PostCreateAsync(createClient, originalName);

        var departmentId = await GetDepartmentIdAsync(originalName);
        using var editClient = CreateClient(factory);
        var response = await PostEditAsync(editClient, departmentId, newName);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var department = await context.Departments.FindAsync(departmentId);
        Assert.NotNull(department);
        Assert.Equal(newName, department.Name);
    }

    [SqlServerIntegrationFact]
    public async Task Edit_BaskaDepartmaninAdinaCakisirsaReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var firstName = $"Kan34-First-{runId}";
        var secondName = $"Kan34-Second-{runId}";
        using var createClient = CreateClient(factory);
        await PostCreateAsync(createClient, firstName);
        await PostCreateAsync(createClient, secondName);

        var secondId = await GetDepartmentIdAsync(secondName);
        using var editClient = CreateClient(factory);
        var response = await PostEditAsync(editClient, secondId, firstName);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("zaten kullan", body);
    }

    [SqlServerIntegrationFact]
    public async Task Deactivate_DepartmaniPasifYaparVeAuditKaydeder()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var name = $"Kan34-Deactivate-{runId}";
        using var createClient = CreateClient(factory);
        await PostCreateAsync(createClient, name);

        var departmentId = await GetDepartmentIdAsync(name);
        using var deactivateClient = CreateClient(factory);
        var response = await PostActionAsync(deactivateClient, "Deactivate", departmentId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var department = await context.Departments.FindAsync(departmentId);
        Assert.NotNull(department);
        Assert.False(department.IsActive);

        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityStatusChanged &&
                    l.TargetEntityType == ActivityEntityTypes.Department &&
                    l.TargetEntityId == departmentId.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(log);
    }

    [SqlServerIntegrationFact]
    public async Task Activate_PasifDepartmaniTekrarAktifYapar()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var name = $"Kan34-Activate-{runId}";
        using var createClient = CreateClient(factory);
        await PostCreateAsync(createClient, name);

        var departmentId = await GetDepartmentIdAsync(name);
        using var deactivateClient = CreateClient(factory);
        await PostActionAsync(deactivateClient, "Deactivate", departmentId);

        using var activateClient = CreateClient(factory);
        var response = await PostActionAsync(activateClient, "Activate", departmentId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var department = await context.Departments.FindAsync(departmentId);
        Assert.NotNull(department);
        Assert.True(department.IsActive);
    }

    [SqlServerIntegrationFact]
    public async Task PasifDepartman_AktifFiltreliSorgudaGorunmez()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var activeName = $"Kan34-Active-{runId}";
        var inactiveName = $"Kan34-Inactive-{runId}";
        using var createClient = CreateClient(factory);
        await PostCreateAsync(createClient, activeName);
        await PostCreateAsync(createClient, inactiveName);

        var inactiveId = await GetDepartmentIdAsync(inactiveName);
        using var deactivateClient = CreateClient(factory);
        await PostActionAsync(deactivateClient, "Deactivate", inactiveId);

        await using var context = CreateRawContext();
        var activeNames = await context.Departments
            .Where(d => d.IsActive)
            .Select(d => d.Name)
            .ToListAsync();

        Assert.Contains(activeName, activeNames);
        Assert.DoesNotContain(inactiveName, activeNames);
    }

    [SqlServerIntegrationFact]
    public async Task ActivityLog_DepartmanOlusturmaKaydeder()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var name = $"Kan34-Audit-{runId}";
        using var client = CreateClient(factory);
        await PostCreateAsync(client, name);

        var departmentId = await GetDepartmentIdAsync(name);
        await using var context = CreateRawContext();
        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityCreated &&
                    l.TargetEntityType == ActivityEntityTypes.Department &&
                    l.TargetEntityId == departmentId.ToString())
            .FirstOrDefaultAsync();

        Assert.NotNull(log);
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
        return factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true
            });
    }

    private async Task<int> GetDepartmentIdAsync(string name)
    {
        await using var context = CreateRawContext();
        var department = await context.Departments.SingleAsync(d => d.Name == name);

        return department.Id;
    }

    private static async Task<HttpResponseMessage> PostCreateAsync(
        HttpClient client,
        string name)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/Departments/Create");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Departments/Create");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Name"] = name
            });

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PostEditAsync(
        HttpClient client,
        int id,
        string name)
    {
        var token = await GetAntiforgeryTokenAsync(client, $"/Departments/Edit/{id}");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Departments/Edit/{id}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = id.ToString(),
                ["Name"] = name
            });

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PostActionAsync(
        HttpClient client,
        string action,
        int id)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/Departments");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/Departments/{action}/{id}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            });

        return await client.SendAsync(request);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);

        var response = await client.SendAsync(request);
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
                    "geçici SQL Server departman entegrasyon testi atlandı.";
            }
        }
    }
}
