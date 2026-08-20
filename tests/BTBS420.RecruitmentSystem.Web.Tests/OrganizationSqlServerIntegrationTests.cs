using System.Net;
using System.Text.RegularExpressions;
using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class OrganizationSqlServerIntegrationTests :
    IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN35_TEST_SQLSERVER_CONNECTION_STRING";

    private readonly TestWebApplicationFactory _baseFactory;

    public OrganizationSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task JobFamily_GecerliIsimIleOlusturulurVeAuditKaydeder()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var name = $"Kan35-JF-{runId}";
        using var client = CreateClient(factory);

        var response = await PostAsync(
            client,
            "/JobFamilies/Create",
            new Dictionary<string, string> { ["Name"] = name });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var jobFamily = await context.JobFamilies.SingleOrDefaultAsync(j => j.Name == name);
        Assert.NotNull(jobFamily);
        Assert.True(jobFamily.IsActive);

        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityCreated &&
                    l.TargetEntityType == ActivityEntityTypes.JobFamily &&
                    l.TargetEntityId == jobFamily.Id.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(log);
    }

    [SqlServerIntegrationFact]
    public async Task JobFamily_AyniIsimIkinciKezReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var name = $"Kan35-JF-Dup-{runId}";
        using var firstClient = CreateClient(factory);
        using var secondClient = CreateClient(factory);

        await PostAsync(
            firstClient,
            "/JobFamilies/Create",
            new Dictionary<string, string> { ["Name"] = name });
        var secondResponse = await PostAsync(
            secondClient,
            "/JobFamilies/Create",
            new Dictionary<string, string> { ["Name"] = name });
        var body = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Contains("zaten kullan", body);
    }

    [SqlServerIntegrationFact]
    public async Task Seniority_GecerliIsimVeSiraIleOlusturulur()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var name = $"Kan35-Sen-{runId}";
        var rank = Random.Shared.Next(10000, 99999);
        using var client = CreateClient(factory);

        var response = await PostAsync(
            client,
            "/Seniorities/Create",
            new Dictionary<string, string> { ["Name"] = name, ["Rank"] = rank.ToString() });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var seniority = await context.Seniorities.SingleOrDefaultAsync(s => s.Name == name);
        Assert.NotNull(seniority);
        Assert.Equal(rank, seniority.Rank);
    }

    [SqlServerIntegrationFact]
    public async Task Seniority_AyniSiraIkinciKezReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var rank = Random.Shared.Next(10000, 99999);
        using var firstClient = CreateClient(factory);
        using var secondClient = CreateClient(factory);

        await PostAsync(
            firstClient,
            "/Seniorities/Create",
            new Dictionary<string, string>
            {
                ["Name"] = $"Kan35-Sen-First-{runId}",
                ["Rank"] = rank.ToString()
            });
        var secondResponse = await PostAsync(
            secondClient,
            "/Seniorities/Create",
            new Dictionary<string, string>
            {
                ["Name"] = $"Kan35-Sen-Second-{runId}",
                ["Rank"] = rank.ToString()
            });
        var body = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Contains("numaras", body);
    }

    [SqlServerIntegrationFact]
    public async Task Position_AktifDepartmanaBaglanarakOlusturulur()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan35-Dept-{runId}");
        var positionName = $"Kan35-Pos-{runId}";
        using var client = CreateClient(factory);

        var response = await PostAsync(
            client,
            "/Positions/Create",
            new Dictionary<string, string>
            {
                ["Name"] = positionName,
                ["DepartmentId"] = departmentId.ToString()
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var position = await context.Positions.SingleOrDefaultAsync(p => p.Name == positionName);
        Assert.NotNull(position);
        Assert.Equal(departmentId, position.DepartmentId);

        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityCreated &&
                    l.TargetEntityType == ActivityEntityTypes.Position &&
                    l.TargetEntityId == position.Id.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(log);
    }

    [SqlServerIntegrationFact]
    public async Task Position_PasifDepartmanaBaglanamaz()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan35-InactiveDept-{runId}");
        await DeactivateDepartmentAsync(factory, departmentId);
        using var client = CreateClient(factory);

        var response = await PostAsync(
            client,
            "/Positions/Create",
            new Dictionary<string, string>
            {
                ["Name"] = $"Kan35-Pos-Inactive-{runId}",
                ["DepartmentId"] = departmentId.ToString()
            });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("aktif de", body);

        await using var context = CreateRawContext();
        var positionExists = await context.Positions
            .AnyAsync(p => p.DepartmentId == departmentId);
        Assert.False(positionExists);
    }

    [SqlServerIntegrationFact]
    public async Task Position_AyniDepartmandaAyniAdReddedilirFarkliDepartmandaKabulEdilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentAId = await CreateDepartmentAsync(factory, $"Kan35-DeptA-{runId}");
        var departmentBId = await CreateDepartmentAsync(factory, $"Kan35-DeptB-{runId}");
        var positionName = $"Kan35-Shared-{runId}";
        using var firstClient = CreateClient(factory);
        using var secondClient = CreateClient(factory);
        using var thirdClient = CreateClient(factory);

        var firstResponse = await PostAsync(
            firstClient,
            "/Positions/Create",
            new Dictionary<string, string>
            {
                ["Name"] = positionName,
                ["DepartmentId"] = departmentAId.ToString()
            });
        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);

        var duplicateResponse = await PostAsync(
            secondClient,
            "/Positions/Create",
            new Dictionary<string, string>
            {
                ["Name"] = positionName,
                ["DepartmentId"] = departmentAId.ToString()
            });
        var duplicateBody = await duplicateResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
        Assert.Contains("zaten var", duplicateBody);

        var differentDepartmentResponse = await PostAsync(
            thirdClient,
            "/Positions/Create",
            new Dictionary<string, string>
            {
                ["Name"] = positionName,
                ["DepartmentId"] = departmentBId.ToString()
            });
        Assert.Equal(HttpStatusCode.Redirect, differentDepartmentResponse.StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task Position_DeactivateVeActivateCalisir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan35-ToggleDept-{runId}");
        var positionName = $"Kan35-Toggle-{runId}";
        using var createClient = CreateClient(factory);
        await PostAsync(
            createClient,
            "/Positions/Create",
            new Dictionary<string, string>
            {
                ["Name"] = positionName,
                ["DepartmentId"] = departmentId.ToString()
            });

        int positionId;
        await using (var context = CreateRawContext())
        {
            positionId = (await context.Positions.SingleAsync(p => p.Name == positionName)).Id;
        }

        using var deactivateClient = CreateClient(factory);
        var deactivateResponse = await PostAsync(
            deactivateClient,
            $"/Positions/Deactivate/{positionId}",
            []);
        Assert.Equal(HttpStatusCode.Redirect, deactivateResponse.StatusCode);

        await using (var context = CreateRawContext())
        {
            var position = await context.Positions.FindAsync(positionId);
            Assert.NotNull(position);
            Assert.False(position.IsActive);
        }

        using var activateClient = CreateClient(factory);
        var activateResponse = await PostAsync(
            activateClient,
            $"/Positions/Activate/{positionId}",
            []);
        Assert.Equal(HttpStatusCode.Redirect, activateResponse.StatusCode);

        await using (var context = CreateRawContext())
        {
            var position = await context.Positions.FindAsync(positionId);
            Assert.NotNull(position);
            Assert.True(position.IsActive);
        }
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

    private static async Task<int> CreateDepartmentAsync(
        WebApplicationFactory<Program> factory,
        string name)
    {
        using var client = CreateClient(factory);
        var response = await PostAsync(
            client,
            "/Departments/Create",
            new Dictionary<string, string> { ["Name"] = name });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var department = await context.Departments.SingleAsync(d => d.Name == name);

        return department.Id;
    }

    private static async Task DeactivateDepartmentAsync(
        WebApplicationFactory<Program> factory,
        int departmentId)
    {
        using var client = CreateClient(factory);
        var response = await PostAsync(client, $"/Departments/Deactivate/{departmentId}", []);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string path,
        Dictionary<string, string> formFields)
    {
        var token = await GetAntiforgeryTokenAsync(client, GetFormUrl(path));
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
        formFields["__RequestVerificationToken"] = token;
        request.Content = new FormUrlEncodedContent(formFields);

        return await client.SendAsync(request);
    }

    private static string GetFormUrl(string postPath)
    {
        if (postPath.Contains("/Create"))
        {
            return postPath;
        }

        var controllerSegment = postPath.Split('/', StringSplitOptions.RemoveEmptyEntries)[0];

        return $"/{controllerSegment}";
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
                    "geçici SQL Server organizasyon entegrasyon testi atlandı.";
            }
        }
    }
}
