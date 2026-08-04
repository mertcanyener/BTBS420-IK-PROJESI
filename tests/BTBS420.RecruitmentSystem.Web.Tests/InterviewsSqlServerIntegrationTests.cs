using System.Net;
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

public sealed class InterviewsSqlServerIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN46_TEST_SQLSERVER_CONNECTION_STRING";

    private readonly TestWebApplicationFactory _baseFactory;

    public InterviewsSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task Details_AdayKendiMulakatiniGorebilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);

        var candidateId = $"kan56-owner-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var candidateClient = CreateClient(factory);
        await CreateCandidateProfileAsync(candidateClient, candidateId);
        await ApplyAsync(candidateClient, candidateId, jobPostingId);

        await using var context = CreateRawContext();
        var applicationId = (await context.JobApplications
            .SingleAsync(a => a.JobPostingId == jobPostingId)).Id;

        using var setupClient2 = CreateClient(factory);
        var start = TruncateToMinute(DateTime.UtcNow.AddDays(3));
        var end = start.AddHours(1);
        var interviewId = await CreateInterviewAndGetIdAsync(
            setupClient2, responsibleUserId, applicationId, InterviewTypes.Online, start, end,
            "https://meet.example.test/kan56", null);

        using var detailsRequest = new HttpRequestMessage(HttpMethod.Get, $"/Interviews/Details/{interviewId}");
        detailsRequest.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        detailsRequest.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateId);

        var response = await candidateClient.SendAsync(detailsRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task Details_AdayBaskasininMulakatiniGoremezNotFoundDoner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);

        var candidateAId = $"kan56-a-{runId}";
        var candidateBId = $"kan56-b-{runId}";
        await CreateCandidateUserAsync(candidateAId);
        await CreateCandidateUserAsync(candidateBId);

        using var clientA = CreateClient(factory);
        await CreateCandidateProfileAsync(clientA, candidateAId);
        await ApplyAsync(clientA, candidateAId, jobPostingId);

        using var clientB = CreateClient(factory);
        await CreateCandidateProfileAsync(clientB, candidateBId);

        await using var context = CreateRawContext();
        var applicationId = (await context.JobApplications
            .SingleAsync(a => a.JobPostingId == jobPostingId)).Id;

        var start = TruncateToMinute(DateTime.UtcNow.AddDays(3));
        var end = start.AddHours(1);
        var interviewId = await CreateInterviewAndGetIdAsync(
            clientA, responsibleUserId, applicationId, InterviewTypes.Online, start, end,
            "https://meet.example.test/kan56", null);

        using var detailsRequest = new HttpRequestMessage(HttpMethod.Get, $"/Interviews/Details/{interviewId}");
        detailsRequest.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        detailsRequest.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateBId);

        var response = await clientB.SendAsync(detailsRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task Details_KapsamDisindakiUzmanGoremezNotFoundDoner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var start = TruncateToMinute(DateTime.UtcNow.AddDays(3));
        var end = start.AddHours(1);
        var interviewId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, start, end,
            "https://meet.example.test/kan56", null);

        var otherRecruiterId = await CreateRecruiterUserAsync(factory, $"kan56-intruder-{runId}", departmentId);

        using var otherClient = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/Interviews/Details/{interviewId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, otherRecruiterId);

        var response = await otherClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task Edit_GecerliZamanGuncellemesiBasarili()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var start = TruncateToMinute(DateTime.UtcNow.AddDays(3));
        var end = start.AddHours(1);
        var interviewId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, start, end,
            "https://meet.example.test/kan56", null);

        var (token, rowVersion) = await GetEditFormTokensAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, interviewId);

        var newStart = start.AddHours(2);
        var newEnd = newStart.AddHours(1);
        var response = await EditInterviewAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, interviewId,
            InterviewTypes.Online, newStart, newEnd, "https://meet.example.test/kan56-updated", null,
            token, rowVersion);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var interview = await context.Interviews.SingleAsync(i => i.Id == interviewId);
        Assert.Equal(newStart, interview.StartAtUtc);
        Assert.Equal(newEnd, interview.EndAtUtc);
    }

    [SqlServerIntegrationFact]
    public async Task Edit_ZamanDegisikligiMevcutKatilimciyaCakisirsaReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var startA = TruncateToMinute(DateTime.UtcNow.AddDays(3));
        var endA = startA.AddHours(1);
        var startB = TruncateToMinute(DateTime.UtcNow.AddDays(10));
        var endB = startB.AddHours(1);

        var interviewAId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, startA, endA,
            "https://meet.example.test/a", null);
        var interviewBId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, startB, endB,
            "https://meet.example.test/b", null);

        var participantId = await CreateRecruiterUserAsync(factory, $"kan56-participant-{runId}", departmentId);

        await AssignParticipantsAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId, interviewAId, [participantId]);
        await AssignParticipantsAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId, interviewBId, [participantId]);

        var (token, rowVersion) = await GetEditFormTokensAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, interviewAId);

        // A'yı B ile çakışacak şekilde taşımayı dene.
        var response = await EditInterviewAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, interviewAId,
            InterviewTypes.Online, startB.AddMinutes(30), endB.AddMinutes(30),
            "https://meet.example.test/a-moved", null, token, rowVersion);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = CreateRawContext();
        var interview = await context.Interviews.SingleAsync(i => i.Id == interviewAId);
        Assert.Equal(startA, interview.StartAtUtc);
        Assert.Equal(endA, interview.EndAtUtc);
    }

    [SqlServerIntegrationFact]
    public async Task Edit_EsZamanliIkiDuzenlemedenBiriConcurrencyIleReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var start = TruncateToMinute(DateTime.UtcNow.AddDays(3));
        var end = start.AddHours(1);
        var interviewId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, start, end,
            "https://meet.example.test/kan56", null);

        using var client1 = CreateClient(factory);
        using var client2 = CreateClient(factory);

        var (token1, rowVersion1) = await GetEditFormTokensAsync(
            client1, SystemRoles.RecruitmentSpecialist, responsibleUserId, interviewId);
        var (token2, rowVersion2) = await GetEditFormTokensAsync(
            client2, SystemRoles.RecruitmentSpecialist, responsibleUserId, interviewId);

        Assert.Equal(rowVersion1, rowVersion2);

        var newStart1 = start.AddHours(2);
        var newStart2 = start.AddHours(4);

        var responses = await Task.WhenAll(
            EditInterviewAsync(
                client1, SystemRoles.RecruitmentSpecialist, responsibleUserId, interviewId,
                InterviewTypes.Online, newStart1, newStart1.AddHours(1), "https://meet.example.test/1", null,
                token1, rowVersion1),
            EditInterviewAsync(
                client2, SystemRoles.RecruitmentSpecialist, responsibleUserId, interviewId,
                InterviewTypes.Online, newStart2, newStart2.AddHours(1), "https://meet.example.test/2", null,
                token2, rowVersion2));

        var redirectCount = responses.Count(response => response.StatusCode == HttpStatusCode.Redirect);
        Assert.Equal(1, redirectCount);

        await using var context = CreateRawContext();
        var interview = await context.Interviews.SingleAsync(i => i.Id == interviewId);
        Assert.True(interview.StartAtUtc == newStart1 || interview.StartAtUtc == newStart2);
    }

    [SqlServerIntegrationFact]
    public async Task Complete_GecerliGecisBasariliVeAuditKaydeder()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var start = TruncateToMinute(DateTime.UtcNow.AddDays(3));
        var interviewId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, start, start.AddHours(1),
            "https://meet.example.test/kan57", null);

        var response = await PostStatusActionAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, interviewId, "Complete");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var interview = await context.Interviews.SingleAsync(i => i.Id == interviewId);
        Assert.Equal(InterviewStatuses.Completed, interview.Status);

        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityStatusChanged &&
                    l.TargetEntityType == ActivityEntityTypes.Interview &&
                    l.TargetEntityId == interviewId.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(log);
    }

    [SqlServerIntegrationFact]
    public async Task Cancel_GecerliGecisBasarili()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var start = TruncateToMinute(DateTime.UtcNow.AddDays(3));
        var interviewId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, start, start.AddHours(1),
            "https://meet.example.test/kan57", null);

        var response = await PostStatusActionAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, interviewId, "Cancel");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var interview = await context.Interviews.SingleAsync(i => i.Id == interviewId);
        Assert.Equal(InterviewStatuses.Cancelled, interview.Status);
    }

    [SqlServerIntegrationFact]
    public async Task Cancel_TamamlanmisMulakatIptalEdilemez()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var start = TruncateToMinute(DateTime.UtcNow.AddDays(3));
        var interviewId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, start, start.AddHours(1),
            "https://meet.example.test/kan57", null);

        await PostStatusActionAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, interviewId, "Complete");

        var response = await PostStatusActionAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, interviewId, "Cancel");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var interview = await context.Interviews.SingleAsync(i => i.Id == interviewId);
        Assert.Equal(InterviewStatuses.Completed, interview.Status);
    }

    [SqlServerIntegrationFact]
    public async Task Postpone_GecerliErtelemeBasarili()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var start = TruncateToMinute(DateTime.UtcNow.AddDays(3));
        var interviewId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, start, start.AddHours(1),
            "https://meet.example.test/kan57", null);

        var newStart = start.AddDays(5);
        var response = await PostponeAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, interviewId,
            newStart, newStart.AddHours(1));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var interview = await context.Interviews.SingleAsync(i => i.Id == interviewId);
        Assert.Equal(newStart, interview.StartAtUtc);
        Assert.Equal(InterviewStatuses.Scheduled, interview.Status);
    }

    [SqlServerIntegrationFact]
    public async Task Postpone_YeniZamanEksikseReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var start = TruncateToMinute(DateTime.UtcNow.AddDays(3));
        var interviewId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, start, start.AddHours(1),
            "https://meet.example.test/kan57", null);

        var response = await PostponeAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, interviewId,
            newStartAtUtc: null, newEndAtUtc: null);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var interview = await context.Interviews.SingleAsync(i => i.Id == interviewId);
        Assert.Equal(start, interview.StartAtUtc);
    }

    [SqlServerIntegrationFact]
    public async Task Postpone_KatilimciCakismasindaReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var startA = TruncateToMinute(DateTime.UtcNow.AddDays(3));
        var startB = TruncateToMinute(DateTime.UtcNow.AddDays(10));

        var interviewAId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, startA, startA.AddHours(1),
            "https://meet.example.test/a", null);
        var interviewBId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, startB, startB.AddHours(1),
            "https://meet.example.test/b", null);

        var participantId = await CreateRecruiterUserAsync(factory, $"kan57-participant-{runId}", departmentId);

        await AssignParticipantsAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId, interviewAId, [participantId]);
        await AssignParticipantsAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId, interviewBId, [participantId]);

        var response = await PostponeAsync(
            setupClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, interviewAId,
            startB.AddMinutes(30), startB.AddMinutes(30).AddHours(1));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var interview = await context.Interviews.SingleAsync(i => i.Id == interviewAId);
        Assert.Equal(startA, interview.StartAtUtc);
    }

    [SqlServerIntegrationFact]
    public async Task Complete_EszamanliIkiIstektenYalnizBiriBasariliOlur()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var start = TruncateToMinute(DateTime.UtcNow.AddDays(3));
        var interviewId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, start, start.AddHours(1),
            "https://meet.example.test/kan57-race", null);

        using var client1 = CreateClient(factory);
        using var client2 = CreateClient(factory);
        var token1 = await GetAntiforgeryTokenForRoleAsync(client1, "/ApplicationsPool", SystemRoles.RecruitmentSpecialist, responsibleUserId);
        var token2 = await GetAntiforgeryTokenForRoleAsync(client2, "/ApplicationsPool", SystemRoles.RecruitmentSpecialist, responsibleUserId);

        var responses = await Task.WhenAll(
            PostStatusActionWithTokenAsync(client1, SystemRoles.RecruitmentSpecialist, responsibleUserId, interviewId, "Complete", token1),
            PostStatusActionWithTokenAsync(client2, SystemRoles.RecruitmentSpecialist, responsibleUserId, interviewId, "Complete", token2));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Redirect, response.StatusCode));

        await using var context = CreateRawContext();
        var interview = await context.Interviews.SingleAsync(i => i.Id == interviewId);
        Assert.Equal(InterviewStatuses.Completed, interview.Status);

        var logCount = await context.ActivityLogs
            .CountAsync(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityStatusChanged &&
                    l.TargetEntityType == ActivityEntityTypes.Interview &&
                    l.TargetEntityId == interviewId.ToString());
        Assert.Equal(1, logCount);
    }

    [SqlServerIntegrationFact]
    public async Task Cancel_EszamanliIkiIstektenYalnizBiriBasariliOlur()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var start = TruncateToMinute(DateTime.UtcNow.AddDays(3));
        var interviewId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, start, start.AddHours(1),
            "https://meet.example.test/kan57-race2", null);

        using var client1 = CreateClient(factory);
        using var client2 = CreateClient(factory);
        var token1 = await GetAntiforgeryTokenForRoleAsync(client1, "/ApplicationsPool", SystemRoles.RecruitmentSpecialist, responsibleUserId);
        var token2 = await GetAntiforgeryTokenForRoleAsync(client2, "/ApplicationsPool", SystemRoles.RecruitmentSpecialist, responsibleUserId);

        var responses = await Task.WhenAll(
            PostStatusActionWithTokenAsync(client1, SystemRoles.RecruitmentSpecialist, responsibleUserId, interviewId, "Cancel", token1),
            PostStatusActionWithTokenAsync(client2, SystemRoles.RecruitmentSpecialist, responsibleUserId, interviewId, "Cancel", token2));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Redirect, response.StatusCode));

        await using var context = CreateRawContext();
        var interview = await context.Interviews.SingleAsync(i => i.Id == interviewId);
        Assert.Equal(InterviewStatuses.Cancelled, interview.Status);
    }

    [SqlServerIntegrationFact]
    public async Task Postpone_EszamanliIkiIstektenBiriConcurrencyIleReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var start = TruncateToMinute(DateTime.UtcNow.AddDays(3));
        var interviewId = await CreateInterviewAndGetIdAsync(
            setupClient, responsibleUserId, applicationId, InterviewTypes.Online, start, start.AddHours(1),
            "https://meet.example.test/kan57-race3", null);

        using var client1 = CreateClient(factory);
        using var client2 = CreateClient(factory);
        var token1 = await GetAntiforgeryTokenForRoleAsync(client1, "/ApplicationsPool", SystemRoles.RecruitmentSpecialist, responsibleUserId);
        var token2 = await GetAntiforgeryTokenForRoleAsync(client2, "/ApplicationsPool", SystemRoles.RecruitmentSpecialist, responsibleUserId);

        var newStart1 = start.AddDays(5);
        var newStart2 = start.AddDays(8);

        var responses = await Task.WhenAll(
            PostponeWithTokenAsync(client1, SystemRoles.RecruitmentSpecialist, responsibleUserId, interviewId, newStart1, newStart1.AddHours(1), token1),
            PostponeWithTokenAsync(client2, SystemRoles.RecruitmentSpecialist, responsibleUserId, interviewId, newStart2, newStart2.AddHours(1), token2));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Redirect, response.StatusCode));

        await using var context = CreateRawContext();
        var interview = await context.Interviews.SingleAsync(i => i.Id == interviewId);
        Assert.True(interview.StartAtUtc == newStart1 || interview.StartAtUtc == newStart2);
    }

    private static async Task<HttpResponseMessage> PostStatusActionAsync(
        HttpClient client,
        string role,
        string userId,
        int interviewId,
        string action)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(client, "/ApplicationsPool", role, userId);
        return await PostStatusActionWithTokenAsync(client, role, userId, interviewId, action, token);
    }

    private static async Task<HttpResponseMessage> PostStatusActionWithTokenAsync(
        HttpClient client,
        string role,
        string userId,
        int interviewId,
        string action,
        string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Interviews/{action}/{interviewId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, userId);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token });

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PostponeAsync(
        HttpClient client,
        string role,
        string userId,
        int interviewId,
        DateTime? newStartAtUtc,
        DateTime? newEndAtUtc)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(client, "/ApplicationsPool", role, userId);
        return await PostponeWithTokenAsync(client, role, userId, interviewId, newStartAtUtc, newEndAtUtc, token);
    }

    private static async Task<HttpResponseMessage> PostponeWithTokenAsync(
        HttpClient client,
        string role,
        string userId,
        int interviewId,
        DateTime? newStartAtUtc,
        DateTime? newEndAtUtc,
        string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Interviews/Postpone/{interviewId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, userId);

        var formFields = new Dictionary<string, string> { ["__RequestVerificationToken"] = token };
        if (newStartAtUtc is not null)
        {
            formFields["newStartAtUtc"] = newStartAtUtc.Value.ToString("yyyy-MM-ddTHH:mm");
        }

        if (newEndAtUtc is not null)
        {
            formFields["newEndAtUtc"] = newEndAtUtc.Value.ToString("yyyy-MM-ddTHH:mm");
        }

        request.Content = new FormUrlEncodedContent(formFields);

        return await client.SendAsync(request);
    }

    private static DateTime TruncateToMinute(DateTime value)
    {
        return value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMinute));
    }

    private static async Task<(string Token, string RowVersion)> GetEditFormTokensAsync(
        HttpClient client,
        string role,
        string userId,
        int interviewId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/Interviews/Edit/{interviewId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, userId);

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
        Assert.True(rowVersionMatch.Success, "RowVersion alanı bulunamadı.");

        return (
            WebUtility.HtmlDecode(tokenMatch.Groups[1].Value),
            WebUtility.HtmlDecode(rowVersionMatch.Groups[1].Value));
    }

    private static async Task<HttpResponseMessage> EditInterviewAsync(
        HttpClient client,
        string role,
        string userId,
        int interviewId,
        string interviewType,
        DateTime startAtUtc,
        DateTime endAtUtc,
        string? onlineMeetingLink,
        string? location,
        string token,
        string rowVersion)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Interviews/Edit/{interviewId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, userId);

        var formFields = new Dictionary<string, string>
        {
            ["Id"] = interviewId.ToString(),
            ["InterviewType"] = interviewType,
            ["StartAtUtc"] = startAtUtc.ToString("yyyy-MM-ddTHH:mm"),
            ["EndAtUtc"] = endAtUtc.ToString("yyyy-MM-ddTHH:mm"),
            ["RowVersion"] = rowVersion,
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
            client,
            "/ApplicationsPool",
            SystemRoles.RecruitmentSpecialist,
            recruiterId);

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
        var candidateId = $"kan56-candidate-{runId}";
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
        var departmentId = await CreateDepartmentAsync(client, $"Kan56-Dept-{runId}");

        var positionName = $"Kan56-Pos-{runId}";
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

        var recruiterUserName = $"kan56-recruiter-{runId}";
        var recruiterId = await CreateRecruiterUserAsync(factory, recruiterUserName, departmentId);

        var jobPostingTitle = $"Kan56-Ilan-{runId}";
        var deadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var jobPostingToken = await GetAntiforgeryTokenForRoleAsync(client, "/JobPostings/Create", SystemRoles.Admin);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/JobPostings/Create"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Title"] = jobPostingTitle,
                    ["Description"] = "Kan-56 entegrasyon testi ilanı.",
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
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)))
            {
                Skip =
                    $"{ConnectionStringEnvironmentVariable} ayarlanmadığı için " +
                    "geçici SQL Server mülakat entegrasyon testi atlandı.";
            }
        }
    }
}
