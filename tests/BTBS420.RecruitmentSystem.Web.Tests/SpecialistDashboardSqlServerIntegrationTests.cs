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

public sealed class SpecialistDashboardSqlServerIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN66_TEST_SQLSERVER_CONNECTION_STRING";

    private readonly TestWebApplicationFactory _baseFactory;

    public SpecialistDashboardSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task Index_YalnizSorumluIlanKapsamiGosterilirKapsamDisiVeriGizlenir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);

        var departmentId = await CreateDepartmentAsync(setupClient, $"Kan66-Dept-{runId}");

        var recruiterA = await BuildRecruiterScenarioAsync(
            setupClient, factory, departmentId, $"a-{runId}");
        var recruiterB = await BuildRecruiterScenarioAsync(
            setupClient, factory, departmentId, $"b-{runId}");

        using var recruiterAClient = CreateClient(factory);
        var response = await GetAsSpecialistAsync(recruiterAClient, "/SpecialistDashboard", recruiterA.RecruiterId);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Kendi kapsamındaki veriler görünür.
        Assert.Contains(recruiterA.JobPostingTitle, content, StringComparison.Ordinal);
        Assert.Contains(recruiterA.CandidateFullName, content, StringComparison.Ordinal);

        // Aynı departmandaki başka bir uzmanın kapsamı gösterilmez.
        Assert.DoesNotContain(recruiterB.JobPostingTitle, content, StringComparison.Ordinal);
        Assert.DoesNotContain(recruiterB.CandidateFullName, content, StringComparison.Ordinal);
    }

    [SqlServerIntegrationFact]
    public async Task Index_PozisyonFiltresiKapsamiDaraltir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);

        var departmentId = await CreateDepartmentAsync(setupClient, $"Kan66-Poz-{runId}");
        var recruiter = await BuildRecruiterScenarioAsync(
            setupClient, factory, departmentId, $"filter-{runId}");

        using var recruiterClient = CreateClient(factory);

        var matchingResponse = await GetAsSpecialistAsync(
            recruiterClient,
            $"/SpecialistDashboard?positionId={recruiter.PositionId}",
            recruiter.RecruiterId);
        var matchingContent = await matchingResponse.Content.ReadAsStringAsync();
        Assert.Contains(recruiter.JobPostingTitle, matchingContent, StringComparison.Ordinal);

        var nonMatchingResponse = await GetAsSpecialistAsync(
            recruiterClient,
            $"/SpecialistDashboard?positionId={recruiter.PositionId + 1_000_000}",
            recruiter.RecruiterId);
        var nonMatchingContent = await nonMatchingResponse.Content.ReadAsStringAsync();

        // Başlık, kapsam içinde olduğu için filtre dropdown'unda hâlâ görünür (doğru davranış);
        // asıl kontrol edilmesi gereken, ilanın liste satırının (Detay linkinin) kaybolmasıdır.
        Assert.DoesNotContain(
            $"/JobPostings/Details/{recruiter.JobPostingId}",
            nonMatchingContent,
            StringComparison.Ordinal);
    }

    private sealed record RecruiterScenario(
        string RecruiterId,
        int PositionId,
        int JobPostingId,
        string JobPostingTitle,
        string CandidateFullName);

    private static async Task<RecruiterScenario> BuildRecruiterScenarioAsync(
        HttpClient client,
        WebApplicationFactory<Program> factory,
        int departmentId,
        string suffix)
    {
        var positionName = $"Kan66-Pos-{suffix}";
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

        var recruiterId = await CreateRecruiterUserAsync(factory, $"kan66-recruiter-{suffix}", departmentId);

        var jobPostingTitle = $"Kan66-Ilan-{suffix}";
        var deadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var jobPostingToken = await GetAntiforgeryTokenForRoleAsync(client, "/JobPostings/Create", SystemRoles.Admin);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/JobPostings/Create"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Title"] = jobPostingTitle,
                    ["Description"] = "Kan-66 entegrasyon testi ilanı.",
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

        var candidateId = $"kan66-cand-{suffix}";
        await CreateCandidateUserAsync(candidateId);
        var candidateFullName = await CreateCandidateProfileAsync(client, candidateId);

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

        await SetApplicationStatusAsync(applicationId, ApplicationStatuses.Interview);

        var interviewToken = await GetAntiforgeryTokenForRoleAsync(
            client, $"/ApplicationsPool/Details/{applicationId}", SystemRoles.RecruitmentSpecialist, recruiterId);
        using (var request = new HttpRequestMessage(
            HttpMethod.Post, $"/ApplicationsPool/CreateInterview/{applicationId}"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
            request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, recruiterId);
            var startAt = DateTime.UtcNow.AddDays(3);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["InterviewType"] = InterviewTypes.InPerson,
                    ["StartAtUtc"] = startAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    ["EndAtUtc"] = startAt.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ss"),
                    ["Location"] = "Kan-66 Test Ofisi",
                    ["__RequestVerificationToken"] = interviewToken
                });
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var offerToken = await GetAntiforgeryTokenForRoleAsync(
            client, $"/ApplicationsPool/Details/{applicationId}", SystemRoles.RecruitmentSpecialist, recruiterId);
        using (var request = new HttpRequestMessage(
            HttpMethod.Post, $"/Offers/Create?applicationId={applicationId}"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
            request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, recruiterId);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Salary"] = "50000",
                    ["StartDate"] = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd"),
                    ["__RequestVerificationToken"] = offerToken
                });
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var offerId = (await context.Offers
            .SingleAsync(offer => offer.JobApplicationId == applicationId)).Id;

        var submitToken = await GetAntiforgeryTokenForRoleAsync(
            client, $"/Offers/Edit/{offerId}", SystemRoles.RecruitmentSpecialist, recruiterId);
        using (var request = new HttpRequestMessage(HttpMethod.Post, $"/Offers/Submit/{offerId}"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
            request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, recruiterId);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string> { ["__RequestVerificationToken"] = submitToken });
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        return new RecruiterScenario(
            recruiterId, positionId, jobPostingId, jobPostingTitle, candidateFullName);
    }

    private static async Task<string> CreateCandidateProfileAsync(HttpClient client, string candidateId)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(
            client, "/CandidateProfile", SystemRoles.Candidate, candidateId);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/CandidateProfile");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateId);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["FirstName"] = "Test",
                ["LastName"] = candidateId,
                ["__RequestVerificationToken"] = token
            });

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

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

    private static async Task<HttpResponseMessage> GetAsSpecialistAsync(
        HttpClient client, string url, string recruiterId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, recruiterId);
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
                    "geçici SQL Server uzman dashboard entegrasyon testi atlandı.";
            }
        }
    }
}
