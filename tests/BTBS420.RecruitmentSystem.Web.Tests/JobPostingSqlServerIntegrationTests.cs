using System.Net;
using System.Text.RegularExpressions;
using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class JobPostingSqlServerIntegrationTests :
    IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN35_TEST_SQLSERVER_CONNECTION_STRING";

    private readonly TestWebApplicationFactory _baseFactory;

    public JobPostingSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task JobPosting_GecerliBilgilerleTaslakOlusurVeAuditKaydeder()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan41-Dept-{runId}");
        var positionId = await CreatePositionAsync(factory, departmentId, $"Kan41-Pos-{runId}");
        var recruiterId = await CreateRecruiterAsync(factory, departmentId, runId);
        var title = $"Kan41-JP-{runId}";
        using var client = CreateClient(factory);

        var response = await PostAsync(
            client,
            "/JobPostings/Create",
            new Dictionary<string, string>
            {
                ["Title"] = title,
                ["Description"] = "İlan açıklaması",
                ["PositionId"] = positionId.ToString(),
                ["ResponsibleUserId"] = recruiterId,
                ["ApplicationDeadline"] = FutureDeadline(30)
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var jobPosting = await context.JobPostings.SingleOrDefaultAsync(j => j.Title == title);
        Assert.NotNull(jobPosting);
        Assert.Equal(JobPostingStatuses.Draft, jobPosting.Status);
        Assert.Equal(positionId, jobPosting.PositionId);
        Assert.Equal(recruiterId, jobPosting.ResponsibleUserId);

        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityCreated &&
                    l.TargetEntityType == ActivityEntityTypes.JobPosting &&
                    l.TargetEntityId == jobPosting.Id.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(log);
    }

    [SqlServerIntegrationFact]
    public async Task JobPosting_GecmisSonBasvuruTarihiReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan41-Dept-{runId}");
        var positionId = await CreatePositionAsync(factory, departmentId, $"Kan41-Pos-{runId}");
        var recruiterId = await CreateRecruiterAsync(factory, departmentId, runId);
        var title = $"Kan41-JP-Past-{runId}";
        using var client = CreateClient(factory);

        var response = await PostAsync(
            client,
            "/JobPostings/Create",
            new Dictionary<string, string>
            {
                ["Title"] = title,
                ["Description"] = "İlan açıklaması",
                ["PositionId"] = positionId.ToString(),
                ["ResponsibleUserId"] = recruiterId,
                ["ApplicationDeadline"] = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
            });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ileri bir tarih", body);

        await using var context = CreateRawContext();
        var exists = await context.JobPostings.AnyAsync(j => j.Title == title);
        Assert.False(exists);
    }

    [SqlServerIntegrationFact]
    public async Task JobPosting_PasifPozisyonaBaglanamaz()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan41-InactiveDept-{runId}");
        var positionId = await CreatePositionAsync(factory, departmentId, $"Kan41-InactivePos-{runId}");
        await DeactivatePositionAsync(factory, positionId);
        var recruiterId = await CreateRecruiterAsync(factory, departmentId, runId);
        var title = $"Kan41-JP-Inactive-{runId}";
        using var client = CreateClient(factory);

        var response = await PostAsync(
            client,
            "/JobPostings/Create",
            new Dictionary<string, string>
            {
                ["Title"] = title,
                ["Description"] = "İlan açıklaması",
                ["PositionId"] = positionId.ToString(),
                ["ResponsibleUserId"] = recruiterId,
                ["ApplicationDeadline"] = FutureDeadline(30)
            });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("aktif de", body);

        await using var context = CreateRawContext();
        var exists = await context.JobPostings.AnyAsync(j => j.Title == title);
        Assert.False(exists);
    }

    [SqlServerIntegrationFact]
    public async Task JobPosting_EszamanliDuzenlemeCakismasiSessizceEzmez()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan41-ConcDept-{runId}");
        var positionId = await CreatePositionAsync(factory, departmentId, $"Kan41-ConcPos-{runId}");
        var recruiterId = await CreateRecruiterAsync(factory, departmentId, runId);
        var title = $"Kan41-JP-Conc-{runId}";
        using var createClient = CreateClient(factory);

        await PostAsync(
            createClient,
            "/JobPostings/Create",
            new Dictionary<string, string>
            {
                ["Title"] = title,
                ["Description"] = "İlan açıklaması",
                ["PositionId"] = positionId.ToString(),
                ["ResponsibleUserId"] = recruiterId,
                ["ApplicationDeadline"] = FutureDeadline(30)
            });

        int jobPostingId;
        await using (var context = CreateRawContext())
        {
            jobPostingId = (await context.JobPostings.SingleAsync(j => j.Title == title)).Id;
        }

        using var firstEditorClient = CreateClient(factory);
        using var secondEditorClient = CreateClient(factory);

        var firstForm = await GetEditFormStateAsync(firstEditorClient, jobPostingId);
        var secondForm = await GetEditFormStateAsync(secondEditorClient, jobPostingId);

        var firstUpdateTitle = $"{title}-First";
        var firstResponse = await PostAsync(
            firstEditorClient,
            $"/JobPostings/Edit/{jobPostingId}",
            new Dictionary<string, string>
            {
                ["Title"] = firstUpdateTitle,
                ["Description"] = "İlan açıklaması",
                ["PositionId"] = positionId.ToString(),
                ["ResponsibleUserId"] = recruiterId,
                ["ApplicationDeadline"] = FutureDeadline(30),
                ["RowVersion"] = firstForm.RowVersion
            },
            firstForm.AntiforgeryToken);
        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);

        var secondUpdateTitle = $"{title}-Second";
        var secondResponse = await PostAsync(
            secondEditorClient,
            $"/JobPostings/Edit/{jobPostingId}",
            new Dictionary<string, string>
            {
                ["Title"] = secondUpdateTitle,
                ["Description"] = "İlan açıklaması",
                ["PositionId"] = positionId.ToString(),
                ["ResponsibleUserId"] = recruiterId,
                ["ApplicationDeadline"] = FutureDeadline(30),
                ["RowVersion"] = secondForm.RowVersion
            },
            secondForm.AntiforgeryToken);
        var secondBody = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Contains("başka biri tarafından güncellendi", secondBody);

        await using var finalContext = CreateRawContext();
        var jobPosting = await finalContext.JobPostings.SingleAsync(j => j.Id == jobPostingId);
        Assert.Equal(firstUpdateTitle, jobPosting.Title);
    }

    private static string FutureDeadline(int daysFromNow)
    {
        return DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysFromNow)).ToString("yyyy-MM-dd");
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

    private static async Task<int> CreatePositionAsync(
        WebApplicationFactory<Program> factory,
        int departmentId,
        string name)
    {
        using var client = CreateClient(factory);
        var response = await PostAsync(
            client,
            "/Positions/Create",
            new Dictionary<string, string>
            {
                ["Name"] = name,
                ["DepartmentId"] = departmentId.ToString()
            });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var position = await context.Positions.SingleAsync(p => p.Name == name);

        return position.Id;
    }

    private static async Task DeactivatePositionAsync(
        WebApplicationFactory<Program> factory,
        int positionId)
    {
        using var client = CreateClient(factory);
        var response = await PostAsync(client, $"/Positions/Deactivate/{positionId}", []);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<string> CreateRecruiterAsync(
        WebApplicationFactory<Program> factory,
        int departmentId,
        string runId)
    {
        using var client = CreateClient(factory);
        var userName = $"kan41-recruiter-{runId}";
        var response = await PostAsync(
            client,
            "/Users/Create",
            new Dictionary<string, string>
            {
                ["UserName"] = userName,
                ["Email"] = $"{userName}@example.test",
                ["DepartmentId"] = departmentId.ToString(),
                ["Role"] = SystemRoles.RecruitmentSpecialist
            });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var user = await context.Users.SingleAsync(u => u.UserName == userName);

        return user.Id;
    }

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string path,
        Dictionary<string, string> formFields,
        string? antiforgeryToken = null)
    {
        var token = antiforgeryToken ?? await GetAntiforgeryTokenAsync(client, GetFormUrl(path));
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
        formFields["__RequestVerificationToken"] = token;
        request.Content = new FormUrlEncodedContent(formFields);

        return await client.SendAsync(request);
    }

    private static string GetFormUrl(string postPath)
    {
        if (postPath.Contains("/Create") || postPath.Contains("/Edit/"))
        {
            return postPath;
        }

        var controllerSegment = postPath.Split('/', StringSplitOptions.RemoveEmptyEntries)[0];

        return $"/{controllerSegment}";
    }

    private static async Task<(string AntiforgeryToken, string RowVersion)> GetEditFormStateAsync(
        HttpClient client,
        int jobPostingId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/JobPostings/Edit/{jobPostingId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        var tokenMatch = Regex.Match(
            content,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(tokenMatch.Success, "Antiforgery form alanı bulunamadı.");

        var rowVersionMatch = Regex.Match(
            content,
            "name=\"RowVersion\"[^>]*value=\"([^\"]*)\"",
            RegexOptions.CultureInvariant);
        Assert.True(rowVersionMatch.Success, "RowVersion gizli alanı bulunamadı.");

        return (
            WebUtility.HtmlDecode(tokenMatch.Groups[1].Value),
            WebUtility.HtmlDecode(rowVersionMatch.Groups[1].Value));
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
                    "geçici SQL Server ilan entegrasyon testi atlandı.";
            }
        }
    }
}
