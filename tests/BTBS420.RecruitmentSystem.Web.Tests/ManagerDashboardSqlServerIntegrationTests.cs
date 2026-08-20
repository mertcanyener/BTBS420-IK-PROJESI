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

public sealed class ManagerDashboardSqlServerIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN67_TEST_SQLSERVER_CONNECTION_STRING";

    private readonly TestWebApplicationFactory _baseFactory;

    public ManagerDashboardSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task Index_DepartmanKapsamiUygulanirKapsamDisiVeriGizlenir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);

        var scenarioA = await BuildScenarioAsync(setupClient, factory, $"a-{runId}");
        var scenarioB = await BuildScenarioAsync(setupClient, factory, $"b-{runId}");

        await SetApplicationStatusAsync(scenarioA.ApplicationId, ApplicationStatuses.Screening);
        await SetApplicationStatusAsync(scenarioB.ApplicationId, ApplicationStatuses.Screening);

        using var managerClient = CreateClient(factory);
        var response = await GetAsManagerAsync(managerClient, "/ManagerDashboard", scenarioA.ManagerId);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(scenarioA.CandidateFullName, content, StringComparison.Ordinal);
        Assert.DoesNotContain(scenarioB.CandidateFullName, content, StringComparison.Ordinal);
        Assert.DoesNotContain($"/JobPostings/Details/{scenarioB.JobPostingId}", content, StringComparison.Ordinal);
    }

    [SqlServerIntegrationFact]
    public async Task Index_KisaListeSadeceOnElemeVeMulakatDurumlarindanTurerFunnelMetrikleriDogruSayar()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);

        var scenario = await BuildScenarioAsync(setupClient, factory, $"funnel-{runId}");

        var newCandidateName = await CreateAndApplyAsync(
            setupClient, factory, scenario.JobPostingId, $"new-{runId}", null);
        var screeningCandidateName = await CreateAndApplyAsync(
            setupClient, factory, scenario.JobPostingId, $"screening-{runId}", ApplicationStatuses.Screening);
        var interviewCandidateName = await CreateAndApplyAsync(
            setupClient, factory, scenario.JobPostingId, $"interview-{runId}", ApplicationStatuses.Interview);
        var hiredCandidateName = await CreateAndApplyAsync(
            setupClient, factory, scenario.JobPostingId, $"hired-{runId}", ApplicationStatuses.Hired);
        await SetApplicationStatusAsync(scenario.ApplicationId, ApplicationStatuses.New);

        using var managerClient = CreateClient(factory);
        var response = await GetAsManagerAsync(managerClient, "/ManagerDashboard", scenario.ManagerId);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(screeningCandidateName, content, StringComparison.Ordinal);
        Assert.Contains(interviewCandidateName, content, StringComparison.Ordinal);
        Assert.DoesNotContain(newCandidateName, content, StringComparison.Ordinal);
        Assert.DoesNotContain(hiredCandidateName, content, StringComparison.Ordinal);

        var metrics = ExtractMetricValues(content);

        // OpenPositions, New, Screening, Interview, Hired, Rejected, Withdrawn
        Assert.Equal(1, metrics[0]);
        Assert.Equal(2, metrics[1]); // scenario.ApplicationId (New) + newCandidateName (New)
        Assert.Equal(1, metrics[2]);
        Assert.Equal(1, metrics[3]);
        Assert.Equal(1, metrics[4]);
        Assert.Equal(0, metrics[5]);
        Assert.Equal(0, metrics[6]);
    }

    [SqlServerIntegrationFact]
    public async Task Index_BekleyenDegerlendirmeEnAzBirKatilimciEksikOldugundaGosterilirDegerlendirilinceKaybolur()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);

        var scenario = await BuildScenarioAsync(setupClient, factory, $"eval-{runId}");
        await SetApplicationStatusAsync(scenario.ApplicationId, ApplicationStatuses.Interview);

        var interviewToken = await GetAntiforgeryTokenForRoleAsync(
            setupClient,
            $"/ApplicationsPool/Details/{scenario.ApplicationId}",
            SystemRoles.RecruitmentSpecialist,
            scenario.RecruiterId);
        int interviewId;
        using (var request = new HttpRequestMessage(
            HttpMethod.Post, $"/ApplicationsPool/CreateInterview/{scenario.ApplicationId}"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
            request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, scenario.RecruiterId);
            var startAt = DateTime.UtcNow.AddDays(-1);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["InterviewType"] = InterviewTypes.InPerson,
                    ["StartAtUtc"] = startAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    ["EndAtUtc"] = startAt.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ss"),
                    ["Location"] = "Kan-67 Test Ofisi",
                    ["__RequestVerificationToken"] = interviewToken
                });
            var response = await setupClient.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            await using var context = CreateRawContext();
            interviewId = (await context.Interviews
                .SingleAsync(interview => interview.JobApplicationId == scenario.ApplicationId)).Id;
        }

        var assignToken = await GetAntiforgeryTokenForRoleAsync(
            setupClient,
            $"/ApplicationsPool/Details/{scenario.ApplicationId}",
            SystemRoles.RecruitmentSpecialist,
            scenario.RecruiterId);
        using (var request = new HttpRequestMessage(
            HttpMethod.Post, $"/ApplicationsPool/AssignParticipants/{scenario.ApplicationId}"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
            request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, scenario.RecruiterId);
            request.Content = new FormUrlEncodedContent(
                new List<KeyValuePair<string, string>>
                {
                    new("interviewId", interviewId.ToString()),
                    new("participantUserIds", scenario.RecruiterId),
                    new("__RequestVerificationToken", assignToken)
                });
            var response = await setupClient.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var completeToken = await GetAntiforgeryTokenForRoleAsync(
            setupClient,
            $"/Interviews/Details/{interviewId}",
            SystemRoles.RecruitmentSpecialist,
            scenario.RecruiterId);
        using (var request = new HttpRequestMessage(HttpMethod.Post, $"/Interviews/Complete/{interviewId}"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
            request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, scenario.RecruiterId);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string> { ["__RequestVerificationToken"] = completeToken });
            var response = await setupClient.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        using var managerClient = CreateClient(factory);
        var beforeEvaluationResponse = await GetAsManagerAsync(
            managerClient, "/ManagerDashboard", scenario.ManagerId);
        var beforeEvaluationContent = await beforeEvaluationResponse.Content.ReadAsStringAsync();

        Assert.Contains(scenario.CandidateFullName, beforeEvaluationContent, StringComparison.Ordinal);
        Assert.Contains(
            $"/Interviews/Details/{interviewId}", beforeEvaluationContent, StringComparison.Ordinal);

        var evaluationToken = await GetAntiforgeryTokenForRoleAsync(
            setupClient,
            $"/Interviews/Details/{interviewId}",
            SystemRoles.RecruitmentSpecialist,
            scenario.RecruiterId);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/InterviewEvaluations/Create"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
            request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, scenario.RecruiterId);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["interviewId"] = interviewId.ToString(),
                    ["competencyScore"] = "4",
                    ["overallScore"] = "4",
                    ["recommendation"] = InterviewEvaluationRecommendations.Positive,
                    ["__RequestVerificationToken"] = evaluationToken
                });
            var response = await setupClient.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var afterEvaluationResponse = await GetAsManagerAsync(
            managerClient, "/ManagerDashboard", scenario.ManagerId);
        var afterEvaluationContent = await afterEvaluationResponse.Content.ReadAsStringAsync();

        Assert.DoesNotContain(
            $"/Interviews/Details/{interviewId}", afterEvaluationContent, StringComparison.Ordinal);
    }

    private sealed record ManagerScenario(
        string ManagerId,
        string RecruiterId,
        int PositionId,
        int JobPostingId,
        int ApplicationId,
        string CandidateFullName);

    private static async Task<ManagerScenario> BuildScenarioAsync(
        HttpClient client,
        WebApplicationFactory<Program> factory,
        string suffix)
    {
        var departmentId = await CreateDepartmentAsync(client, $"Kan67-Dept-{suffix}");

        var positionName = $"Kan67-Pos-{suffix}";
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

        var recruiterId = await CreateStaffUserAsync(
            factory, $"kan67-recruiter-{suffix}", SystemRoles.RecruitmentSpecialist, departmentId);
        var managerId = await CreateStaffUserAsync(
            factory, $"kan67-manager-{suffix}", SystemRoles.HiringManager, departmentId);

        var jobPostingTitle = $"Kan67-Ilan-{suffix}";
        var deadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var jobPostingToken = await GetAntiforgeryTokenForRoleAsync(client, "/JobPostings/Create", SystemRoles.Admin);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/JobPostings/Create"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Title"] = jobPostingTitle,
                    ["Description"] = "Kan-67 entegrasyon testi ilanı.",
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

        var candidateFullName = await CreateAndApplyAsync(client, factory, jobPostingId, suffix, null);
        var applicationId = (await context.JobApplications
            .SingleAsync(application => application.CandidateProfile.ApplicationUserId == $"kan67-cand-{suffix}"))
            .Id;

        return new ManagerScenario(
            managerId, recruiterId, positionId, jobPostingId, applicationId, candidateFullName);
    }

    private static async Task<string> CreateAndApplyAsync(
        HttpClient client,
        WebApplicationFactory<Program> factory,
        int jobPostingId,
        string suffix,
        string? status)
    {
        var candidateId = $"kan67-cand-{suffix}";
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

        if (status is not null)
        {
            await using var context = CreateRawContext();
            var applicationId = (await context.JobApplications
                .SingleAsync(application => application.CandidateProfile.ApplicationUserId == candidateId)).Id;
            await SetApplicationStatusAsync(applicationId, status);
        }

        return $"Test {candidateId}";
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

    private static async Task<int> CreateDepartmentAsync(HttpClient client, string departmentName)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(client, "/Departments/Create", SystemRoles.Admin);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Departments/Create");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
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

    private static async Task<string> CreateStaffUserAsync(
        WebApplicationFactory<Program> factory,
        string userName,
        string role,
        int departmentId)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
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

        var roleResult = await userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", roleResult.Errors.Select(error => error.Description)));
        }

        return user.Id;
    }

    private static async Task<HttpResponseMessage> GetAsManagerAsync(
        HttpClient client, string url, string managerId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.HiringManager);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, managerId);
        return await client.SendAsync(request);
    }

    private static int[] ExtractMetricValues(string html)
    {
        var matches = Regex.Matches(html, "<p class=\"h3 mb-0\">(\\d+)</p>", RegexOptions.CultureInvariant);
        Assert.Equal(7, matches.Count);
        return matches.Select(match => int.Parse(match.Groups[1].Value)).ToArray();
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
                    "geçici SQL Server yönetici dashboard entegrasyon testi atlandı.";
            }
        }
    }
}
