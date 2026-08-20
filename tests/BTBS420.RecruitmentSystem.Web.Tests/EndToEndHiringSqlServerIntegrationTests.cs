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

public sealed class EndToEndHiringSqlServerIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN46_TEST_SQLSERVER_CONNECTION_STRING";

    private readonly TestWebApplicationFactory _baseFactory;

    public EndToEndHiringSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task KabulAkisi_KayittanIseAlindiyaKadarTamZincir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, recruiterId) =
            await CreatePublishedJobPostingAsync(setupClient, factory, runId);

        var candidateId = $"kan28-accept-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var candidateClient = CreateClient(factory);
        await CreateCandidateProfileAsync(candidateClient, candidateId);
        await ApplyAsync(candidateClient, candidateId, jobPostingId);

        await using var context = CreateRawContext();
        var applicationId = (await context.JobApplications
            .SingleAsync(a => a.CandidateProfile.ApplicationUserId == candidateId)).Id;

        // Uygulamada başvuruyu Mülakat aşamasına taşıyan ayrı bir HTTP action yok (bu geçiş
        // yalnızca mülakat planlanmasıyla ima ediliyor); diğer tüm entegrasyon testlerinde
        // kullanılan aynı kısayolla aşamayı ilerletiyoruz.
        await SetApplicationStatusAsync(applicationId, ApplicationStatuses.Interview);

        using var recruiterClient = CreateClient(factory);
        var start = DateTime.UtcNow.AddDays(3);
        var interviewId = await CreateInterviewAndGetIdAsync(
            recruiterClient, recruiterId, applicationId, InterviewTypes.Online, start, start.AddHours(1),
            "https://meet.example.test/kan28-accept", null);

        var panelistId = await CreateRecruiterUserAsync(factory, $"kan28-panelist-accept-{runId}", departmentId);
        await AssignParticipantsAsync(
            recruiterClient, SystemRoles.RecruitmentSpecialist, recruiterId, applicationId, interviewId, [panelistId]);

        var completeResponse = await PostStatusActionAsync(
            recruiterClient, SystemRoles.RecruitmentSpecialist, recruiterId, interviewId, "Complete");
        Assert.Equal(HttpStatusCode.Redirect, completeResponse.StatusCode);

        using var panelistClient = CreateClient(factory);
        var evaluationResponse = await CreateEvaluationAsync(
            panelistClient, panelistId, interviewId, "Güçlü aday.", 4, 4,
            InterviewEvaluationRecommendations.Positive);
        Assert.Equal(HttpStatusCode.Redirect, evaluationResponse.StatusCode);

        var offerResponse = await CreateOfferAsync(
            recruiterClient,
            SystemRoles.RecruitmentSpecialist,
            recruiterId,
            applicationId,
            75000m,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(21)),
            "Kan-28 uçtan uca kabul akışı teklifi.");
        Assert.Equal(HttpStatusCode.Redirect, offerResponse.StatusCode);

        await using var offerContext = CreateRawContext();
        var offerId = (await offerContext.Offers.SingleAsync(o => o.JobApplicationId == applicationId)).Id;

        await SubmitOfferAsync(recruiterClient, SystemRoles.RecruitmentSpecialist, recruiterId, offerId);

        var managerId = await CreateHiringManagerUserAsync(factory, $"kan28-manager-accept-{runId}", departmentId);
        using var managerClient = CreateClient(factory);
        await ApproveOfferAsync(managerClient, SystemRoles.HiringManager, managerId, offerId);

        var decisionResponse = await AcceptOfferAsync(candidateClient, candidateId, applicationId);
        Assert.Equal(HttpStatusCode.Redirect, decisionResponse.StatusCode);

        await using var verifyContext = CreateRawContext();
        var finalApplication = await verifyContext.JobApplications.SingleAsync(a => a.Id == applicationId);
        Assert.Equal(ApplicationStatuses.Hired, finalApplication.Status);

        var finalOffer = await verifyContext.Offers.SingleAsync(o => o.Id == offerId);
        Assert.Equal(OfferStatuses.Accepted, finalOffer.Status);

        var candidateNotificationCount = await verifyContext.Notifications
            .CountAsync(n => n.RecipientUserId == candidateId);
        Assert.True(
            candidateNotificationCount >= 2,
            "Aday, mülakat planlandı ve teklif onaylandı bildirimlerini almış olmalı.");

        var recruiterDecisionNotification = await verifyContext.Notifications.SingleOrDefaultAsync(
            n => n.RecipientUserId == recruiterId &&
                 n.EventKey == $"offer-status-changed:{offerId}:{OfferStatuses.Accepted}");
        Assert.NotNull(recruiterDecisionNotification);

        var applicationAuditLog = await verifyContext.ActivityLogs.SingleOrDefaultAsync(
            l => l.TargetEntityType == ActivityEntityTypes.Application &&
                 l.TargetEntityId == applicationId.ToString() &&
                 l.ActionCode == ActivityActionCodes.EntityStatusChanged);
        Assert.NotNull(applicationAuditLog);

        using var adminClient = CreateClient(factory);
        var dashboardResponse = await GetAsAdminAsync(adminClient, $"/AdminDashboard?departmentId={departmentId}");
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        var metrics = ExtractMetricValues(await dashboardResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, metrics[2]); // TotalApplications
        Assert.Equal(0, metrics[3]); // InProgressApplications (İşe Alındı terminal durumdur)
        Assert.Equal(1, metrics[4]); // HiredCount
    }

    [SqlServerIntegrationFact]
    public async Task RetAkisi_TeklifAdayTarafindanReddedilirBasvuruReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, recruiterId) =
            await CreatePublishedJobPostingAsync(setupClient, factory, runId);

        var candidateId = $"kan28-reject-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var candidateClient = CreateClient(factory);
        await CreateCandidateProfileAsync(candidateClient, candidateId);
        await ApplyAsync(candidateClient, candidateId, jobPostingId);

        await using var context = CreateRawContext();
        var applicationId = (await context.JobApplications
            .SingleAsync(a => a.CandidateProfile.ApplicationUserId == candidateId)).Id;

        await SetApplicationStatusAsync(applicationId, ApplicationStatuses.Interview);

        using var recruiterClient = CreateClient(factory);
        var start = DateTime.UtcNow.AddDays(3);
        var interviewId = await CreateInterviewAndGetIdAsync(
            recruiterClient, recruiterId, applicationId, InterviewTypes.Online, start, start.AddHours(1),
            "https://meet.example.test/kan28-reject", null);

        var panelistId = await CreateRecruiterUserAsync(factory, $"kan28-panelist-reject-{runId}", departmentId);
        await AssignParticipantsAsync(
            recruiterClient, SystemRoles.RecruitmentSpecialist, recruiterId, applicationId, interviewId, [panelistId]);

        var completeResponse = await PostStatusActionAsync(
            recruiterClient, SystemRoles.RecruitmentSpecialist, recruiterId, interviewId, "Complete");
        Assert.Equal(HttpStatusCode.Redirect, completeResponse.StatusCode);

        using var panelistClient = CreateClient(factory);
        var evaluationResponse = await CreateEvaluationAsync(
            panelistClient, panelistId, interviewId, "Ortalama aday.", 3, 3,
            InterviewEvaluationRecommendations.Positive);
        Assert.Equal(HttpStatusCode.Redirect, evaluationResponse.StatusCode);

        var offerResponse = await CreateOfferAsync(
            recruiterClient,
            SystemRoles.RecruitmentSpecialist,
            recruiterId,
            applicationId,
            60000m,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(21)),
            "Kan-28 uçtan uca ret akışı teklifi.");
        Assert.Equal(HttpStatusCode.Redirect, offerResponse.StatusCode);

        await using var offerContext = CreateRawContext();
        var offerId = (await offerContext.Offers.SingleAsync(o => o.JobApplicationId == applicationId)).Id;

        await SubmitOfferAsync(recruiterClient, SystemRoles.RecruitmentSpecialist, recruiterId, offerId);

        var managerId = await CreateHiringManagerUserAsync(factory, $"kan28-manager-reject-{runId}", departmentId);
        using var managerClient = CreateClient(factory);
        await ApproveOfferAsync(managerClient, SystemRoles.HiringManager, managerId, offerId);

        var decisionResponse = await RejectOfferAsync(candidateClient, candidateId, applicationId);
        Assert.Equal(HttpStatusCode.Redirect, decisionResponse.StatusCode);

        await using var verifyContext = CreateRawContext();
        var finalApplication = await verifyContext.JobApplications.SingleAsync(a => a.Id == applicationId);
        Assert.Equal(ApplicationStatuses.Rejected, finalApplication.Status);

        var finalOffer = await verifyContext.Offers.SingleAsync(o => o.Id == offerId);
        Assert.Equal(OfferStatuses.RejectedByCandidate, finalOffer.Status);

        var candidateNotificationCount = await verifyContext.Notifications
            .CountAsync(n => n.RecipientUserId == candidateId);
        Assert.True(
            candidateNotificationCount >= 2,
            "Aday, mülakat planlandı ve teklif onaylandı bildirimlerini almış olmalı.");

        var recruiterDecisionNotification = await verifyContext.Notifications.SingleOrDefaultAsync(
            n => n.RecipientUserId == recruiterId &&
                 n.EventKey == $"offer-status-changed:{offerId}:{OfferStatuses.RejectedByCandidate}");
        Assert.NotNull(recruiterDecisionNotification);

        var applicationAuditLog = await verifyContext.ActivityLogs.SingleOrDefaultAsync(
            l => l.TargetEntityType == ActivityEntityTypes.Application &&
                 l.TargetEntityId == applicationId.ToString() &&
                 l.ActionCode == ActivityActionCodes.EntityStatusChanged);
        Assert.NotNull(applicationAuditLog);

        using var adminClient = CreateClient(factory);
        var dashboardResponse = await GetAsAdminAsync(adminClient, $"/AdminDashboard?departmentId={departmentId}");
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        var metrics = ExtractMetricValues(await dashboardResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, metrics[2]); // TotalApplications
        Assert.Equal(0, metrics[3]); // InProgressApplications (Reddedildi terminal durumdur)
        Assert.Equal(0, metrics[4]); // HiredCount
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

    private static async Task<HttpResponseMessage> SubmitOfferAsync(
        HttpClient client,
        string role,
        string userId,
        int offerId)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(client, $"/Offers/Edit/{offerId}", role, userId);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Offers/Submit/{offerId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, userId);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token });

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> ApproveOfferAsync(
        HttpClient client,
        string role,
        string userId,
        int offerId)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(client, $"/Offers/Edit/{offerId}", role, userId);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Offers/Approve/{offerId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, userId);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token });

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> AcceptOfferAsync(
        HttpClient client,
        string candidateId,
        int applicationId)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(client, "/JobApplications", SystemRoles.Candidate, candidateId);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/JobApplications/AcceptOffer/{applicationId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateId);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token });

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> RejectOfferAsync(
        HttpClient client,
        string candidateId,
        int applicationId)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(client, "/JobApplications", SystemRoles.Candidate, candidateId);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/JobApplications/RejectOffer/{applicationId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateId);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token });

        return await client.SendAsync(request);
    }

    private static async Task<int> CreateInterviewAndGetIdAsync(
        HttpClient client,
        string recruiterId,
        int applicationId,
        string interviewType,
        DateTime startAtUtc,
        DateTime endAtUtc,
        string? onlineMeetingLink,
        string? location)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(
            client, "/ApplicationsPool", SystemRoles.RecruitmentSpecialist, recruiterId);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/ApplicationsPool/CreateInterview/{applicationId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, recruiterId);

        var formFields = new Dictionary<string, string>
        {
            ["InterviewType"] = interviewType,
            ["StartAtUtc"] = startAtUtc.ToString("yyyy-MM-ddTHH:mm"),
            ["EndAtUtc"] = endAtUtc.ToString("yyyy-MM-ddTHH:mm"),
            ["__RequestVerificationToken"] = token
        };

        if (onlineMeetingLink is not null)
        {
            formFields["OnlineMeetingLink"] = onlineMeetingLink;
        }

        if (location is not null)
        {
            formFields["Location"] = location;
        }

        request.Content = new FormUrlEncodedContent(formFields);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        return await context.Interviews
            .Where(interview => interview.JobApplicationId == applicationId)
            .OrderByDescending(interview => interview.Id)
            .Select(interview => interview.Id)
            .FirstAsync();
    }

    private static async Task<HttpResponseMessage> AssignParticipantsAsync(
        HttpClient client,
        string actorRole,
        string actorUserId,
        int applicationId,
        int interviewId,
        IEnumerable<string> participantUserIds)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(client, "/ApplicationsPool", actorRole, actorUserId);

        var formFields = new List<KeyValuePair<string, string>>
        {
            new("interviewId", interviewId.ToString()),
            new("__RequestVerificationToken", token)
        };

        foreach (var participantId in participantUserIds)
        {
            formFields.Add(new KeyValuePair<string, string>("participantUserIds", participantId));
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/ApplicationsPool/AssignParticipants/{applicationId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, actorRole);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, actorUserId);
        request.Content = new FormUrlEncodedContent(formFields);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return response;
    }

    private static async Task<HttpResponseMessage> PostStatusActionAsync(
        HttpClient client,
        string role,
        string userId,
        int interviewId,
        string action)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(client, "/ApplicationsPool", role, userId);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Interviews/{action}/{interviewId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, userId);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token });

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> CreateEvaluationAsync(
        HttpClient client,
        string panelistId,
        int interviewId,
        string? note,
        int competencyScore,
        int overallScore,
        string recommendation)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(
            client, "/ApplicationsPool", SystemRoles.RecruitmentSpecialist, panelistId);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/InterviewEvaluations/Create");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, panelistId);

        var formFields = new Dictionary<string, string>
        {
            ["interviewId"] = interviewId.ToString(),
            ["competencyScore"] = competencyScore.ToString(),
            ["overallScore"] = overallScore.ToString(),
            ["recommendation"] = recommendation,
            ["__RequestVerificationToken"] = token
        };

        if (note is not null)
        {
            formFields["note"] = note;
        }

        request.Content = new FormUrlEncodedContent(formFields);
        return await client.SendAsync(request);
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
            string runId)
    {
        var departmentId = await CreateDepartmentAsync(client, $"Kan28-Dept-{runId}");

        var positionName = $"Kan28-Pos-{runId}";
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

        var recruiterId = await CreateRecruiterUserAsync(factory, $"kan28-recruiter-{runId}", departmentId);

        var jobPostingTitle = $"Kan28-Ilan-{runId}";
        var deadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var jobPostingToken = await GetAntiforgeryTokenForRoleAsync(client, "/JobPostings/Create", SystemRoles.Admin);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/JobPostings/Create"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Title"] = jobPostingTitle,
                    ["Description"] = "Kan-28 uçtan uca entegrasyon testi ilanı.",
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

    private static async Task<string> CreateHiringManagerUserAsync(
        WebApplicationFactory<Program> factory,
        string userName,
        int departmentId)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync(SystemRoles.HiringManager))
        {
            await roleManager.CreateAsync(new IdentityRole(SystemRoles.HiringManager));
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

        var roleResult = await userManager.AddToRoleAsync(user, SystemRoles.HiringManager);
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
                    "geçici SQL Server uçtan uca entegrasyon testi atlandı.";
            }
        }
    }
}
