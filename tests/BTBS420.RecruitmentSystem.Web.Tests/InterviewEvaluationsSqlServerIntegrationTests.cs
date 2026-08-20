using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class InterviewEvaluationsSqlServerIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN46_TEST_SQLSERVER_CONNECTION_STRING";

    private readonly TestWebApplicationFactory _baseFactory;

    public InterviewEvaluationsSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task Create_GecerliDegerlendirmeOlusurVeAuditKaydeder()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var start = DateTime.UtcNow.AddDays(3);
        var interviewId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, start, start.AddHours(1),
            "https://meet.example.test/kan59", null);

        var panelistId = await CreateRecruiterUserAsync(factory, $"kan59-panelist-{runId}", departmentId);
        await AssignParticipantsAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId, interviewId, [panelistId]);

        var response = await CreateEvaluationAsync(
            setupClient, panelistId, interviewId, "İyi bir aday.", 4, 5, InterviewEvaluationRecommendations.Positive);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var evaluation = await context.InterviewEvaluations
            .SingleAsync(e => e.InterviewId == interviewId && e.EvaluatorUserId == panelistId);
        Assert.Equal(4, evaluation.CompetencyScore);
        Assert.Equal(5, evaluation.OverallScore);
        Assert.Equal(InterviewEvaluationRecommendations.Positive, evaluation.Recommendation);

        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityCreated &&
                    l.TargetEntityType == ActivityEntityTypes.Interview &&
                    l.TargetEntityId == interviewId.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(log);
    }

    [SqlServerIntegrationFact]
    public async Task Create_AyniMulakatIcinIkinciKezOlusturulamaz()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var start = DateTime.UtcNow.AddDays(3);
        var interviewId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, start, start.AddHours(1),
            "https://meet.example.test/kan59", null);

        var panelistId = await CreateRecruiterUserAsync(factory, $"kan59-dup-{runId}", departmentId);
        await AssignParticipantsAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId, interviewId, [panelistId]);

        await CreateEvaluationAsync(
            setupClient, panelistId, interviewId, "İlk not.", 3, 3, InterviewEvaluationRecommendations.Positive);
        await CreateEvaluationAsync(
            setupClient, panelistId, interviewId, "İkinci not.", 5, 5, InterviewEvaluationRecommendations.Negative);

        await using var context = CreateRawContext();
        var count = await context.InterviewEvaluations
            .CountAsync(e => e.InterviewId == interviewId && e.EvaluatorUserId == panelistId);
        Assert.Equal(1, count);

        var evaluation = await context.InterviewEvaluations
            .SingleAsync(e => e.InterviewId == interviewId && e.EvaluatorUserId == panelistId);
        Assert.Equal("İlk not.", evaluation.Note);
    }

    [SqlServerIntegrationFact]
    public async Task Create_GecersizPuanReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var start = DateTime.UtcNow.AddDays(3);
        var interviewId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, start, start.AddHours(1),
            "https://meet.example.test/kan59", null);

        var panelistId = await CreateRecruiterUserAsync(factory, $"kan59-badscore-{runId}", departmentId);
        await AssignParticipantsAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId, interviewId, [panelistId]);

        await CreateEvaluationAsync(
            setupClient, panelistId, interviewId, "Not.", 6, 5, InterviewEvaluationRecommendations.Positive);

        await using var context = CreateRawContext();
        var count = await context.InterviewEvaluations
            .CountAsync(e => e.InterviewId == interviewId && e.EvaluatorUserId == panelistId);
        Assert.Equal(0, count);
    }

    [SqlServerIntegrationFact]
    public async Task Create_PanelUyesiOlmayanOlusturamazNotFoundDoner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var start = DateTime.UtcNow.AddDays(3);
        var interviewId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, start, start.AddHours(1),
            "https://meet.example.test/kan59", null);

        var outsiderId = await CreateRecruiterUserAsync(factory, $"kan59-outsider-{runId}", departmentId);

        var response = await CreateEvaluationAsync(
            setupClient, outsiderId, interviewId, "Not.", 3, 3, InterviewEvaluationRecommendations.Positive);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var context = CreateRawContext();
        var count = await context.InterviewEvaluations.CountAsync(e => e.InterviewId == interviewId);
        Assert.Equal(0, count);
    }

    [SqlServerIntegrationFact]
    public async Task Edit_SahibiKendiKaydiniGunceller()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var start = DateTime.UtcNow.AddDays(3);
        var interviewId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, start, start.AddHours(1),
            "https://meet.example.test/kan59", null);

        var panelistId = await CreateRecruiterUserAsync(factory, $"kan59-owner-{runId}", departmentId);
        await AssignParticipantsAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId, interviewId, [panelistId]);

        await CreateEvaluationAsync(
            setupClient, panelistId, interviewId, "İlk not.", 3, 3, InterviewEvaluationRecommendations.Negative);

        await using var context = CreateRawContext();
        var evaluationId = (await context.InterviewEvaluations
            .SingleAsync(e => e.InterviewId == interviewId && e.EvaluatorUserId == panelistId)).Id;

        var response = await EditEvaluationAsync(
            setupClient, panelistId, evaluationId, "Güncellenmiş not.", 5, 5, InterviewEvaluationRecommendations.Positive);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var verificationContext = CreateRawContext();
        var evaluation = await verificationContext.InterviewEvaluations.SingleAsync(e => e.Id == evaluationId);
        Assert.Equal("Güncellenmiş not.", evaluation.Note);
        Assert.Equal(5, evaluation.CompetencyScore);
        Assert.Equal(InterviewEvaluationRecommendations.Positive, evaluation.Recommendation);
    }

    [SqlServerIntegrationFact]
    public async Task Edit_BaskasininKaydiniGuncelleyemezNotFoundDoner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var start = DateTime.UtcNow.AddDays(3);
        var interviewId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, start, start.AddHours(1),
            "https://meet.example.test/kan59", null);

        var panelistId = await CreateRecruiterUserAsync(factory, $"kan59-owner2-{runId}", departmentId);
        var intruderId = await CreateRecruiterUserAsync(factory, $"kan59-intruder-{runId}", departmentId);
        await AssignParticipantsAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId, interviewId,
            [panelistId, intruderId]);

        await CreateEvaluationAsync(
            setupClient, panelistId, interviewId, "İlk not.", 3, 3, InterviewEvaluationRecommendations.Negative);

        await using var context = CreateRawContext();
        var evaluationId = (await context.InterviewEvaluations
            .SingleAsync(e => e.InterviewId == interviewId && e.EvaluatorUserId == panelistId)).Id;

        var response = await EditEvaluationAsync(
            setupClient, intruderId, evaluationId, "Yetkisiz güncelleme.", 1, 1, InterviewEvaluationRecommendations.Negative);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var verificationContext = CreateRawContext();
        var evaluation = await verificationContext.InterviewEvaluations.SingleAsync(e => e.Id == evaluationId);
        Assert.Equal("İlk not.", evaluation.Note);
    }

    [SqlServerIntegrationFact]
    public async Task Delete_SahibiSilebilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var start = DateTime.UtcNow.AddDays(3);
        var interviewId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, start, start.AddHours(1),
            "https://meet.example.test/kan59", null);

        var panelistId = await CreateRecruiterUserAsync(factory, $"kan59-deleter-{runId}", departmentId);
        await AssignParticipantsAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId, interviewId, [panelistId]);

        await CreateEvaluationAsync(
            setupClient, panelistId, interviewId, "Silinecek not.", 3, 3, InterviewEvaluationRecommendations.Negative);

        await using var context = CreateRawContext();
        var evaluationId = (await context.InterviewEvaluations
            .SingleAsync(e => e.InterviewId == interviewId && e.EvaluatorUserId == panelistId)).Id;

        var response = await DeleteEvaluationAsync(setupClient, panelistId, evaluationId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var verificationContext = CreateRawContext();
        var stillExists = await verificationContext.InterviewEvaluations.AnyAsync(e => e.Id == evaluationId);
        Assert.False(stillExists);
    }

    [SqlServerIntegrationFact]
    public async Task Delete_BaskasininKaydiniSilemezNotFoundDoner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var start = DateTime.UtcNow.AddDays(3);
        var interviewId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, start, start.AddHours(1),
            "https://meet.example.test/kan59", null);

        var panelistId = await CreateRecruiterUserAsync(factory, $"kan59-owner3-{runId}", departmentId);
        var intruderId = await CreateRecruiterUserAsync(factory, $"kan59-intruder2-{runId}", departmentId);
        await AssignParticipantsAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId, interviewId,
            [panelistId, intruderId]);

        await CreateEvaluationAsync(
            setupClient, panelistId, interviewId, "Silinmemesi gereken not.", 3, 3, InterviewEvaluationRecommendations.Negative);

        await using var context = CreateRawContext();
        var evaluationId = (await context.InterviewEvaluations
            .SingleAsync(e => e.InterviewId == interviewId && e.EvaluatorUserId == panelistId)).Id;

        var response = await DeleteEvaluationAsync(setupClient, intruderId, evaluationId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var verificationContext = CreateRawContext();
        var stillExists = await verificationContext.InterviewEvaluations.AnyAsync(e => e.Id == evaluationId);
        Assert.True(stillExists);
    }

    [SqlServerIntegrationFact]
    public async Task Edit_EszamanliIkiIstektenBiriConcurrencyIleReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var start = DateTime.UtcNow.AddDays(3);
        var interviewId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, start, start.AddHours(1),
            "https://meet.example.test/kan59-race", null);

        var panelistId = await CreateRecruiterUserAsync(factory, $"kan59-race-{runId}", departmentId);
        await AssignParticipantsAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId, interviewId, [panelistId]);

        await CreateEvaluationAsync(
            setupClient, panelistId, interviewId, "İlk not.", 3, 3, InterviewEvaluationRecommendations.Negative);

        await using var setupContext = CreateRawContext();
        var evaluationId = (await setupContext.InterviewEvaluations
            .SingleAsync(e => e.InterviewId == interviewId && e.EvaluatorUserId == panelistId)).Id;

        using var client1 = CreateClient(factory);
        using var client2 = CreateClient(factory);
        var token1 = await GetAntiforgeryTokenForRoleAsync(client1, "/ApplicationsPool", SystemRoles.RecruitmentSpecialist, panelistId);
        var token2 = await GetAntiforgeryTokenForRoleAsync(client2, "/ApplicationsPool", SystemRoles.RecruitmentSpecialist, panelistId);

        var responses = await Task.WhenAll(
            EditEvaluationWithTokenAsync(
                client1, panelistId, evaluationId, "Birinci güncelleme.", 4, 4,
                InterviewEvaluationRecommendations.Positive, token1),
            EditEvaluationWithTokenAsync(
                client2, panelistId, evaluationId, "İkinci güncelleme.", 5, 5,
                InterviewEvaluationRecommendations.Negative, token2));

        var redirectCount = responses.Count(response => response.StatusCode == HttpStatusCode.Redirect);
        Assert.Equal(2, redirectCount);

        await using var context = CreateRawContext();
        var evaluation = await context.InterviewEvaluations.SingleAsync(e => e.Id == evaluationId);
        Assert.True(evaluation.Note == "Birinci güncelleme." || evaluation.Note == "İkinci güncelleme.");
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

    private static async Task<HttpResponseMessage> EditEvaluationAsync(
        HttpClient client,
        string userId,
        int evaluationId,
        string? note,
        int competencyScore,
        int overallScore,
        string recommendation)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(
            client, "/ApplicationsPool", SystemRoles.RecruitmentSpecialist, userId);

        return await EditEvaluationWithTokenAsync(
            client, userId, evaluationId, note, competencyScore, overallScore, recommendation, token);
    }

    private static async Task<HttpResponseMessage> EditEvaluationWithTokenAsync(
        HttpClient client,
        string userId,
        int evaluationId,
        string? note,
        int competencyScore,
        int overallScore,
        string recommendation,
        string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/InterviewEvaluations/Edit/{evaluationId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, userId);

        var formFields = new Dictionary<string, string>
        {
            ["id"] = evaluationId.ToString(),
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

    private static async Task<HttpResponseMessage> DeleteEvaluationAsync(
        HttpClient client,
        string userId,
        int evaluationId)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(
            client, "/ApplicationsPool", SystemRoles.RecruitmentSpecialist, userId);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/InterviewEvaluations/Delete/{evaluationId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, userId);
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

    private static async Task<int> CreateApplicationAsync(
        WebApplicationFactory<Program> factory,
        HttpClient setupClient,
        string runId,
        int jobPostingId)
    {
        var candidateId = $"kan59-candidate-{runId}";
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
        var token = await GetAntiforgeryTokenForRoleAsync(client, "/CandidateProfile", SystemRoles.Candidate, candidateId);

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
        var profile = await context.CandidateProfiles.SingleAsync(p => p.ApplicationUserId == candidateId);

        return profile.Id;
    }

    private static async Task<HttpResponseMessage> ApplyAsync(HttpClient client, string candidateId, int jobPostingId)
    {
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
        var departmentId = await CreateDepartmentAsync(client, $"Kan59-Dept-{runId}");

        var positionName = $"Kan59-Pos-{runId}";
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

        var recruiterUserName = $"kan59-recruiter-{runId}";
        var recruiterId = await CreateRecruiterUserAsync(factory, recruiterUserName, departmentId);

        var jobPostingTitle = $"Kan59-Ilan-{runId}";
        var deadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var jobPostingToken = await GetAntiforgeryTokenForRoleAsync(client, "/JobPostings/Create", SystemRoles.Admin);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/JobPostings/Create"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Title"] = jobPostingTitle,
                    ["Description"] = "Kan-59 entegrasyon testi ilanı.",
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

        return WebUtility.HtmlDecode(tokenMatch.Groups[1].Value);
    }

    private WebApplicationFactory<Program> CreateSqlFactory()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)!;

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

    private static ApplicationDbContext CreateRawContext()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)!;
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
                    Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)))
            {
                Skip =
                    $"{ConnectionStringEnvironmentVariable} ayarlanmadığı için " +
                    "geçici SQL Server mülakat değerlendirme testi atlandı.";
            }
        }
    }
}
