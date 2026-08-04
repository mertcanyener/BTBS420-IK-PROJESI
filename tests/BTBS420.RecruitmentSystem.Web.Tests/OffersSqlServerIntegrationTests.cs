using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using BTBS420.RecruitmentSystem.Web.ActivityLogging;
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

public sealed class OffersSqlServerIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN61_TEST_SQLSERVER_CONNECTION_STRING";

    private readonly TestWebApplicationFactory _baseFactory;

    public OffersSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task Create_MulakatDurumundakiBasvuruIcinTaslakOlusturur()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, recruiterId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);
        await SetApplicationStatusAsync(applicationId, ApplicationStatuses.Interview);

        using var recruiterClient = CreateClient(factory);
        var response = await CreateOfferAsync(
            recruiterClient,
            SystemRoles.RecruitmentSpecialist,
            recruiterId,
            applicationId,
            50000m,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
            "Kan61 test teklifi.");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var offer = await context.Offers.SingleAsync(o => o.JobApplicationId == applicationId);
        Assert.Equal(OfferStatuses.Draft, offer.Status);
        Assert.Equal(50000m, offer.Salary);
        Assert.Equal(recruiterId, offer.CreatedByUserId);

        var activityLog = await context.ActivityLogs.SingleOrDefaultAsync(
            log => log.TargetEntityType == ActivityEntityTypes.Offer && log.TargetEntityId == offer.Id.ToString());
        Assert.NotNull(activityLog);
        Assert.Equal(ActivityActionCodes.EntityCreated, activityLog!.ActionCode);
    }

    [SqlServerIntegrationFact]
    public async Task Create_IkinciTeklifDenemesiBasarisizOlur()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, recruiterId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);
        await SetApplicationStatusAsync(applicationId, ApplicationStatuses.Interview);

        using var recruiterClient = CreateClient(factory);
        var firstResponse = await CreateOfferAsync(
            recruiterClient,
            SystemRoles.RecruitmentSpecialist,
            recruiterId,
            applicationId,
            50000m,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
            null);
        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);

        var secondResponse = await CreateOfferAsync(
            recruiterClient,
            SystemRoles.RecruitmentSpecialist,
            recruiterId,
            applicationId,
            60000m,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(21)),
            null);
        Assert.Equal(HttpStatusCode.Redirect, secondResponse.StatusCode);

        await using var context = CreateRawContext();
        var offerCount = await context.Offers.CountAsync(o => o.JobApplicationId == applicationId);
        Assert.Equal(1, offerCount);
    }

    [SqlServerIntegrationFact]
    public async Task Create_MulakatDisindakiDurumIcinBasarisizOlur()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, recruiterId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        using var recruiterClient = CreateClient(factory);
        var response = await CreateOfferAsync(
            recruiterClient,
            SystemRoles.RecruitmentSpecialist,
            recruiterId,
            applicationId,
            50000m,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
            null);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var offerCount = await context.Offers.CountAsync(o => o.JobApplicationId == applicationId);
        Assert.Equal(0, offerCount);
    }

    [SqlServerIntegrationFact]
    public async Task Create_KapsamDisindakiUzmanErisemezNotFoundDoner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (ownJobPostingId, departmentId, _) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var (otherJobPostingId, _, _) = await CreatePublishedJobPostingAsync(
            setupClient,
            factory,
            $"other-{runId}");

        var intruderId = await CreateRecruiterUserAsync(factory, $"kan61-intruder-{runId}", departmentId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, otherJobPostingId);
        await SetApplicationStatusAsync(applicationId, ApplicationStatuses.Interview);

        using var intruderClient = CreateClient(factory);
        var response = await CreateOfferAsync(
            intruderClient,
            SystemRoles.RecruitmentSpecialist,
            intruderId,
            applicationId,
            50000m,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
            null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task Create_AdminHerBasvuruIcinOlusturabilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, _) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);
        await SetApplicationStatusAsync(applicationId, ApplicationStatuses.Interview);
        var adminUserId = await CreateAdminUserAsync(factory, $"kan61-admin-{runId}", departmentId);

        using var adminClient = CreateClient(factory);
        var response = await CreateOfferAsync(
            adminClient,
            SystemRoles.Admin,
            adminUserId,
            applicationId,
            50000m,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
            null);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task Edit_TaslakBilgileriniGunceller()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, recruiterId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);
        await SetApplicationStatusAsync(applicationId, ApplicationStatuses.Interview);

        using var recruiterClient = CreateClient(factory);
        await CreateOfferAsync(
            recruiterClient,
            SystemRoles.RecruitmentSpecialist,
            recruiterId,
            applicationId,
            50000m,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
            "İlk not.");

        await using var context = CreateRawContext();
        var offerId = (await context.Offers.SingleAsync(o => o.JobApplicationId == applicationId)).Id;

        var editResponse = await EditOfferAsync(
            recruiterClient,
            SystemRoles.RecruitmentSpecialist,
            recruiterId,
            offerId,
            65000m,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            "Güncellenmiş not.");

        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);

        await using var verifyContext = CreateRawContext();
        var offer = await verifyContext.Offers.SingleAsync(o => o.Id == offerId);
        Assert.Equal(65000m, offer.Salary);
        Assert.Equal("Güncellenmiş not.", offer.Note);
    }

    [SqlServerIntegrationFact]
    public async Task Details_TaslakAdayaGoruntulenemez()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, recruiterId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);
        await SetApplicationStatusAsync(applicationId, ApplicationStatuses.Interview);

        using var recruiterClient = CreateClient(factory);
        await CreateOfferAsync(
            recruiterClient,
            SystemRoles.RecruitmentSpecialist,
            recruiterId,
            applicationId,
            50000m,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
            null);

        await using var context = CreateRawContext();
        var offer = await context.Offers.SingleAsync(o => o.JobApplicationId == applicationId);
        var candidateUserId = (await context.JobApplications
            .Include(a => a.CandidateProfile)
            .SingleAsync(a => a.Id == applicationId)).CandidateProfile.ApplicationUserId;

        using var candidateClient = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/Offers/Edit/{offer.Id}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateUserId);

        var response = await candidateClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task SetApplicationStatusAsync(int applicationId, string status)
    {
        await using var context = CreateRawContext();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE JobApplications SET Status = {status} WHERE Id = {applicationId}");
    }

    private static async Task<HttpResponseMessage> CreateOfferAsync(
        HttpClient client,
        string role,
        string userId,
        int applicationId,
        decimal salary,
        DateOnly startDate,
        string? note)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(
            client,
            $"/ApplicationsPool/Details/{applicationId}",
            role,
            userId);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/Offers/Create?applicationId={applicationId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, userId);

        var formFields = new Dictionary<string, string>
        {
            ["Salary"] = salary.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["StartDate"] = startDate.ToString("yyyy-MM-dd"),
            ["__RequestVerificationToken"] = token
        };

        if (note is not null)
        {
            formFields["Note"] = note;
        }

        request.Content = new FormUrlEncodedContent(formFields);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> EditOfferAsync(
        HttpClient client,
        string role,
        string userId,
        int offerId,
        decimal salary,
        DateOnly startDate,
        string? note)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(client, $"/Offers/Edit/{offerId}", role, userId);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Offers/Edit/{offerId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, userId);

        var formFields = new Dictionary<string, string>
        {
            ["Salary"] = salary.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["StartDate"] = startDate.ToString("yyyy-MM-dd"),
            ["__RequestVerificationToken"] = token
        };

        if (note is not null)
        {
            formFields["Note"] = note;
        }

        await using var context = CreateRawContext();
        var rowVersion = Convert.ToBase64String(
            (await context.Offers.SingleAsync(o => o.Id == offerId)).RowVersion);
        formFields["RowVersion"] = rowVersion;

        request.Content = new FormUrlEncodedContent(formFields);
        return await client.SendAsync(request);
    }

    private static async Task<int> CreateApplicationAsync(
        WebApplicationFactory<Program> factory,
        HttpClient setupClient,
        string runId,
        int jobPostingId)
    {
        var candidateId = $"kan61-candidate-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var candidateClient = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(candidateClient, candidateId);
        await ApplyAsync(candidateClient, candidateId, jobPostingId);

        await using var context = CreateRawContext();
        return (await context.JobApplications.SingleAsync(a => a.CandidateProfileId == profileId)).Id;
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

    private static async Task<int> CreateCandidateProfileAsync(HttpClient client, string candidateId)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(
            client,
            "/CandidateProfile",
            SystemRoles.Candidate,
            candidateId);

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

        await using var context = CreateRawContext();
        var profile = await context.CandidateProfiles
            .SingleAsync(p => p.ApplicationUserId == candidateId);

        return profile.Id;
    }

    private static async Task<HttpResponseMessage> ApplyAsync(
        HttpClient client,
        string candidateId,
        int jobPostingId)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(
            client,
            $"/PublicJobPostings/Details/{jobPostingId}",
            SystemRoles.Candidate,
            candidateId);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/JobApplications/Create");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateId);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["jobPostingId"] = jobPostingId.ToString(),
                ["__RequestVerificationToken"] = token
            });

        return await client.SendAsync(request);
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

    private static async Task<(int JobPostingId, int DepartmentId, string ResponsibleUserId)>
        CreatePublishedJobPostingAsync(
            HttpClient client,
            WebApplicationFactory<Program> factory,
            string runId,
            string? existingResponsibleUserId = null)
    {
        var departmentId = await CreateDepartmentAsync(client, $"Kan61-Dept-{runId}");

        var positionName = $"Kan61-Pos-{runId}";
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

        var recruiterId = existingResponsibleUserId ?? await CreateRecruiterUserAsync(
            factory,
            $"kan61-recruiter-{runId}",
            departmentId);

        var jobPostingTitle = $"Kan61-Ilan-{runId}";
        var deadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var jobPostingToken = await GetAntiforgeryTokenForRoleAsync(client, "/JobPostings/Create", SystemRoles.Admin);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/JobPostings/Create"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Title"] = jobPostingTitle,
                    ["Description"] = "Kan-61 entegrasyon testi ilanı.",
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
            client,
            $"/JobPostings/Details/{jobPostingId}",
            SystemRoles.Admin);
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

    private static async Task<string> CreateAdminUserAsync(
        WebApplicationFactory<Program> factory,
        string userName,
        int departmentId)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync(SystemRoles.Admin))
        {
            await roleManager.CreateAsync(new IdentityRole(SystemRoles.Admin));
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

        var roleResult = await userManager.AddToRoleAsync(user, SystemRoles.Admin);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", roleResult.Errors.Select(error => error.Description)));
        }

        return user.Id;
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

        return WebUtility.HtmlDecode(tokenMatch.Groups[1].Value);
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
                    "geçici SQL Server teklif entegrasyon testi atlandı.";
            }
        }
    }
}
