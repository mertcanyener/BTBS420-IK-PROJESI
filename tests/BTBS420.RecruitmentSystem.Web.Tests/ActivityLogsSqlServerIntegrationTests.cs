using System.Net;
using System.Text.RegularExpressions;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class ActivityLogsSqlServerIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN68_TEST_SQLSERVER_CONNECTION_STRING";

    private readonly TestWebApplicationFactory _baseFactory;

    public ActivityLogsSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task Export_FiltrelenmisTumSonucuIcerirSayfalamaSinirlamaz()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var client = CreateClient(factory);

        var actorId = $"kan68-actor-{runId}";
        const int totalDepartments = 27; // varsayılan sayfa boyutu (25) aşılıyor

        for (var index = 0; index < totalDepartments; index++)
        {
            await CreateDepartmentAsync(client, $"Kan68-Dept-{runId}-{index}", actorId);
        }

        using var adminClient = CreateClient(factory);

        var indexResponse = await GetAsAdminAsync(adminClient, $"/ActivityLogs?userId={actorId}");
        var indexContent = await indexResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);

        var totalCountMatch = Regex.Match(indexContent, @"Toplam (\d+) kayıt\.");
        Assert.True(totalCountMatch.Success, "Toplam kayıt sayısı bulunamadı.");
        Assert.Equal(totalDepartments, int.Parse(totalCountMatch.Groups[1].Value));

        // Razor, @entry.Summary gibi C# ifadelerinden gelen Türkçe karakterleri (ş/ı/ö vb.)
        // HTML entity'sine kodluyor (örn. "ş" -> "&#x15F;"), bu yüzden görüntülenen satır
        // sayısını metin eşleşmesiyle değil, <tbody> içindeki <tr> sayısıyla ölçüyoruz
        // (kodlamadan bağımsız, sağlam bir sinyal). CSV Razor'dan geçmediği için
        // (CsvExportHelper doğrudan bayt üretiyor) literal metinle karşılaştırılabilir.
        var tbodyMatch = Regex.Match(indexContent, "<tbody>(.*?)</tbody>", RegexOptions.Singleline);
        Assert.True(tbodyMatch.Success, "Tablo gövdesi bulunamadı.");
        var displayedRowCount = Regex.Matches(tbodyMatch.Groups[1].Value, "<tr>").Count;
        Assert.Equal(25, displayedRowCount); // sayfa 1, varsayılan sayfa boyutu

        var exportResponse = await GetAsAdminAsync(adminClient, $"/ActivityLogs/Export?userId={actorId}");
        Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);
        Assert.Equal("text/csv", exportResponse.Content.Headers.ContentType?.MediaType);

        var csvContent = await exportResponse.Content.ReadAsStringAsync();
        var exportedRowCount = Regex.Matches(csvContent, "Departman oluşturuldu\\.").Count;
        Assert.Equal(totalDepartments, exportedRowCount); // export sayfalamaya tabi değil
    }

    [SqlServerIntegrationFact]
    public async Task Index_BirlesikFiltrelerDogruCalisir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var client = CreateClient(factory);

        var scenario = await BuildRejectedApplicationScenarioAsync(client, factory, runId);

        // Alakasız gürültü: farklı bir aktörün departman oluşturma kaydı.
        await CreateDepartmentAsync(client, $"Kan68-Noise-{runId}", $"kan68-noise-actor-{runId}");

        using var adminClient = CreateClient(factory);

        var matchingResponse = await GetAsAdminAsync(
            adminClient,
            $"/ActivityLogs?userId={scenario.RecruiterId}&jobPostingId={scenario.JobPostingId}" +
            $"&candidateId={scenario.CandidateUserId}&actionCode=entity.status-changed");
        var matchingContent = await matchingResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, matchingResponse.StatusCode);
        Assert.Contains(scenario.JobPostingTitle, matchingContent, StringComparison.Ordinal);
        Assert.Contains(scenario.CandidateFullName, matchingContent, StringComparison.Ordinal);

        var mismatchedResponse = await GetAsAdminAsync(
            adminClient,
            $"/ActivityLogs?userId={scenario.RecruiterId}&jobPostingId={scenario.JobPostingId + 1_000_000}" +
            $"&candidateId={scenario.CandidateUserId}&actionCode=entity.status-changed");
        var mismatchedContent = await mismatchedResponse.Content.ReadAsStringAsync();

        // Aday adı, kapsam içinde olduğu için filtre dropdown'unda hâlâ görünebilir (doğru
        // davranış); asıl kontrol edilmesi gereken, sonuç satırının kaybolmasıdır.
        var mismatchedTotalCountMatch = Regex.Match(mismatchedContent, @"Toplam (\d+) kayıt\.");
        Assert.True(mismatchedTotalCountMatch.Success, "Toplam kayıt sayısı bulunamadı.");
        Assert.Equal(0, int.Parse(mismatchedTotalCountMatch.Groups[1].Value));
    }

    [SqlServerIntegrationFact]
    public async Task UserDetails_AktiviteKayitlarinaBaglantiIcerir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var client = CreateClient(factory);

        var scenario = await BuildRejectedApplicationScenarioAsync(client, factory, runId);

        using var adminClient = CreateClient(factory);
        var response = await GetAsAdminAsync(adminClient, $"/Users/Details/{scenario.RecruiterId}");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            $"/ActivityLogs?userId={scenario.RecruiterId}", content, StringComparison.Ordinal);
    }

    private sealed record RejectedApplicationScenario(
        string RecruiterId,
        int JobPostingId,
        string JobPostingTitle,
        string CandidateUserId,
        string CandidateFullName);

    private static async Task<RejectedApplicationScenario> BuildRejectedApplicationScenarioAsync(
        HttpClient client,
        WebApplicationFactory<Program> factory,
        string runId)
    {
        var departmentId = await CreateDepartmentAsync(client, $"Kan68-Dept-{runId}", null);

        var positionName = $"Kan68-Pos-{runId}";
        var positionToken = await GetAntiforgeryTokenForRoleAsync(client, "/Positions/Create", SystemRoles.Admin);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/Positions/Create"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Name"] = positionName,
                    ["DepartmentId"] = departmentId.ToString(),
                    ["__RequestVerificationToken"] = positionToken
                });
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        await using var context = CreateRawContext();
        var positionId = (await context.Positions.SingleAsync(p => p.Name == positionName)).Id;

        var recruiterId = await CreateRecruiterUserAsync(factory, $"kan68-recruiter-{runId}", departmentId);

        var jobPostingTitle = $"Kan68-Ilan-{runId}";
        var deadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var jobPostingToken = await GetAntiforgeryTokenForRoleAsync(client, "/JobPostings/Create", SystemRoles.Admin);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/JobPostings/Create"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Title"] = jobPostingTitle,
                    ["Description"] = "Kan-68 entegrasyon testi ilanı.",
                    ["PositionId"] = positionId.ToString(),
                    ["ResponsibleUserId"] = recruiterId,
                    ["ApplicationDeadline"] = deadline.ToString("yyyy-MM-dd"),
                    ["__RequestVerificationToken"] = jobPostingToken
                });
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var jobPostingId = (await context.JobPostings.SingleAsync(j => j.Title == jobPostingTitle)).Id;

        var statusToken = await GetAntiforgeryTokenForRoleAsync(
            client, $"/JobPostings/Details/{jobPostingId}", SystemRoles.Admin);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/JobPostings/ChangeStatus"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["id"] = jobPostingId.ToString(),
                    ["newStatus"] = JobPostingStatuses.Published,
                    ["__RequestVerificationToken"] = statusToken
                });
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var candidateId = $"kan68-cand-{runId}";
        await CreateCandidateUserAsync(candidateId);

        var profileToken = await GetAntiforgeryTokenForRoleAsync(
            client, "/CandidateProfile", SystemRoles.Candidate, candidateId);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/CandidateProfile"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
            request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateId);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["FirstName"] = "Test",
                    ["LastName"] = candidateId,
                    ["__RequestVerificationToken"] = profileToken
                });
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var applyToken = await GetAntiforgeryTokenForRoleAsync(
            client, $"/PublicJobPostings/Details/{jobPostingId}", SystemRoles.Candidate, candidateId);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/JobApplications/Create"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
            request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateId);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["jobPostingId"] = jobPostingId.ToString(),
                    ["__RequestVerificationToken"] = applyToken
                });
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var applicationId = (await context.JobApplications
            .SingleAsync(application => application.CandidateProfile.ApplicationUserId == candidateId)).Id;

        await SetApplicationStatusAsync(applicationId, ApplicationStatuses.Screening);

        var rejectToken = await GetAntiforgeryTokenForRoleAsync(
            client,
            $"/ApplicationsPool/Details/{applicationId}",
            SystemRoles.RecruitmentSpecialist,
            recruiterId);
        using (var request = new HttpRequestMessage(HttpMethod.Post, $"/ApplicationsPool/Reject/{applicationId}"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
            request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, recruiterId);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["reason"] = "Kan-68 entegrasyon testi reddi.",
                    ["__RequestVerificationToken"] = rejectToken
                });
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        return new RejectedApplicationScenario(
            recruiterId, jobPostingId, jobPostingTitle, candidateId, $"Test {candidateId}");
    }

    private static async Task CreateCandidateUserAsync(string candidateId)
    {
        await using var context = CreateRawContext();
        context.Users.Add(
            new ApplicationUser
            {
                Id = candidateId,
                UserName = candidateId,
                NormalizedUserName = candidateId.ToUpperInvariant(),
                Email = $"{candidateId}@example.test",
                NormalizedEmail = $"{candidateId}@example.test".ToUpperInvariant()
            });
        await context.SaveChangesAsync();
    }

    private static async Task SetApplicationStatusAsync(int applicationId, string status)
    {
        await using var context = CreateRawContext();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE JobApplications SET Status = {status} WHERE Id = {applicationId}");
    }

    private static async Task<int> CreateDepartmentAsync(HttpClient client, string departmentName, string? actorId)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(
            client, "/Departments/Create", SystemRoles.Admin, actorId);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Departments/Create");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
        if (actorId is not null)
        {
            request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, actorId);
        }

        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Name"] = departmentName,
                ["__RequestVerificationToken"] = token
            });
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        return (await context.Departments.SingleAsync(d => d.Name == departmentName)).Id;
    }

    private static async Task<string> CreateRecruiterUserAsync(
        WebApplicationFactory<Program> factory,
        string userName,
        int departmentId)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync(SystemRoles.RecruitmentSpecialist))
        {
            await roleManager.CreateAsync(new IdentityRole(SystemRoles.RecruitmentSpecialist));
        }

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = $"{userName}@example.test",
            EmailConfirmed = true,
            DepartmentId = departmentId
        };

        var createResult = await userManager.CreateAsync(user, "P@ssw0rd_Test123!");
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", createResult.Errors.Select(error => error.Description)));
        }

        var roleResult = await userManager.AddToRoleAsync(user, SystemRoles.RecruitmentSpecialist);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", roleResult.Errors.Select(error => error.Description)));
        }

        return user.Id;
    }

    private static async Task<HttpResponseMessage> GetAsAdminAsync(HttpClient client, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
        return await client.SendAsync(request);
    }

    private static async Task<string> GetAntiforgeryTokenForRoleAsync(
        HttpClient client,
        string url,
        string role,
        string? userId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        if (userId is not null)
        {
            request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, userId);
        }

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        var tokenMatch = Regex.Match(
            content,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(tokenMatch.Success, $"Antiforgery form alanı bulunamadı ({url}).");

        return System.Net.WebUtility.HtmlDecode(tokenMatch.Groups[1].Value);
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
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<INotificationPublisher>();
                services.AddScoped<INotificationPublisher>(
                    serviceProvider => serviceProvider.GetRequiredService<NotificationService>());
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
                    "geçici SQL Server aktivite kayıtları entegrasyon testi atlandı.";
            }
        }
    }
}
