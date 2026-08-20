using System.Net;
using System.Net.Http.Headers;
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

public sealed class AdminDashboardSqlServerIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN65_TEST_SQLSERVER_CONNECTION_STRING";

    private readonly TestWebApplicationFactory _baseFactory;

    public AdminDashboardSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task Index_BosKapsamdaBesMetrikSifirGuvenliDoner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var client = CreateClient(factory);

        var departmentId = await CreateDepartmentAsync(client, $"Kan65-Bos-{runId}");

        var response = await GetAsAdminAsync(
            client, $"/AdminDashboard?departmentId={departmentId}");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new[] { 0, 0, 0, 0, 0 }, ExtractMetricValues(content));
    }

    [SqlServerIntegrationFact]
    public async Task Index_DepartmanFiltresiBesMetrigiDogruHesaplar()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);

        var (jobPostingIdA, departmentIdA, _) = await CreatePublishedJobPostingAsync(
            setupClient, factory, $"a-{runId}");
        var (_, departmentIdB, _) = await CreatePublishedJobPostingAsync(
            setupClient, factory, $"b-{runId}");

        var newApplicationId = await ApplyAsCandidateAsync(
            setupClient, factory, $"kan65-cand-new-{runId}", jobPostingIdA);
        var interviewApplicationId = await ApplyAsCandidateAsync(
            setupClient, factory, $"kan65-cand-int-{runId}", jobPostingIdA);
        await SetApplicationStatusAsync(interviewApplicationId, ApplicationStatuses.Interview);
        var hiredApplicationId = await ApplyAsCandidateAsync(
            setupClient, factory, $"kan65-cand-hired-{runId}", jobPostingIdA);
        await SetApplicationStatusAsync(hiredApplicationId, ApplicationStatuses.Hired);

        // Diğer departmanda da veri var; filtre bunu dışlamalı.
        await ApplyAsCandidateAsync(
            setupClient, factory, $"kan65-cand-other-{runId}", jobPostingIdA);
        _ = newApplicationId;

        using var adminClient = CreateClient(factory);
        var response = await GetAsAdminAsync(
            adminClient, $"/AdminDashboard?departmentId={departmentIdA}");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var metrics = ExtractMetricValues(content);

        // TotalUsers, ActiveJobPostings, TotalApplications, InProgress, Hired
        Assert.Equal(1, metrics[0]); // yalnızca A departmanının uzmanı
        Assert.Equal(1, metrics[1]); // yalnızca A departmanının yayımlanmış ilanı
        Assert.Equal(4, metrics[2]); // A ilanına yapılan 4 başvuru (new+new+interview+hired)
        Assert.Equal(3, metrics[3]); // new + new + interview
        Assert.Equal(1, metrics[4]); // hired

        var departmentBOnlyResponse = await GetAsAdminAsync(
            adminClient, $"/AdminDashboard?departmentId={departmentIdB}");
        var departmentBOnlyContent = await departmentBOnlyResponse.Content.ReadAsStringAsync();
        var departmentBMetrics = ExtractMetricValues(departmentBOnlyContent);

        Assert.Equal(0, departmentBMetrics[2]);
        Assert.Equal(0, departmentBMetrics[3]);
        Assert.Equal(0, departmentBMetrics[4]);
    }

    [SqlServerIntegrationFact]
    public async Task Index_TarihFiltresiBasvurulariDaraltirAmaKullaniciSayisiniEtkilemez()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);

        var (jobPostingId, departmentId, _) = await CreatePublishedJobPostingAsync(
            setupClient, factory, runId);
        await ApplyAsCandidateAsync(setupClient, factory, $"kan65-cand-date-{runId}", jobPostingId);

        using var adminClient = CreateClient(factory);
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));

        var response = await GetAsAdminAsync(
            adminClient, $"/AdminDashboard?departmentId={departmentId}&dateFrom={futureDate:yyyy-MM-dd}");
        var content = await response.Content.ReadAsStringAsync();
        var metrics = ExtractMetricValues(content);

        Assert.Equal(1, metrics[0]); // kullanıcı sayısı tarih filtresinden etkilenmez
        Assert.Equal(0, metrics[2]); // ama başvuru sayısı gelecekteki tarih filtresiyle 0'a düşer
    }

    private static async Task<HttpResponseMessage> GetAsAdminAsync(HttpClient client, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
        return await client.SendAsync(request);
    }

    private static int[] ExtractMetricValues(string html)
    {
        var matches = Regex.Matches(html, "<p class=\"h3 mb-0\">(\\d+)</p>", RegexOptions.CultureInvariant);
        Assert.Equal(5, matches.Count);
        return matches.Select(match => int.Parse(match.Groups[1].Value)).ToArray();
    }

    private static async Task<int> ApplyAsCandidateAsync(
        HttpClient client,
        WebApplicationFactory<Program> factory,
        string candidateId,
        int jobPostingId)
    {
        await CreateCandidateUserAsync(candidateId);
        await CreateCandidateProfileAsync(client, candidateId);

        var token = await GetAntiforgeryTokenForRoleAsync(
            client, $"/PublicJobPostings/Details/{jobPostingId}", SystemRoles.Candidate, candidateId);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/JobApplications/Create");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateId);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["jobPostingId"] = jobPostingId.ToString(),
                ["__RequestVerificationToken"] = token
            });

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        return (await context.JobApplications
            .SingleAsync(application => application.CandidateProfile.ApplicationUserId == candidateId))
            .Id;
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

    private static async Task CreateCandidateProfileAsync(HttpClient client, string candidateId)
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
                ["LastName"] = "Aday",
                ["__RequestVerificationToken"] = token
            });

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
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

    private static async Task<(int JobPostingId, int DepartmentId, string RecruiterId)>
        CreatePublishedJobPostingAsync(
            HttpClient client,
            WebApplicationFactory<Program> factory,
            string runId)
    {
        var departmentId = await CreateDepartmentAsync(client, $"Kan65-Dept-{runId}");

        var positionName = $"Kan65-Pos-{runId}";
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

        var recruiterId = await CreateRecruiterUserAsync(factory, $"kan65-recruiter-{runId}", departmentId);

        var jobPostingTitle = $"Kan65-Ilan-{runId}";
        var deadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var jobPostingToken = await GetAntiforgeryTokenForRoleAsync(client, "/JobPostings/Create", SystemRoles.Admin);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/JobPostings/Create"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Title"] = jobPostingTitle,
                    ["Description"] = "Kan-65 entegrasyon testi ilanı.",
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

        return (jobPostingId, departmentId, recruiterId);
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
                    "geçici SQL Server admin dashboard entegrasyon testi atlandı.";
            }
        }
    }
}
