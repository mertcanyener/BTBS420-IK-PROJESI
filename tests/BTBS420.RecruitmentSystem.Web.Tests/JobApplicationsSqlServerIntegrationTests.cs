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

public sealed class JobApplicationsSqlServerIntegrationTests :
    IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN46_TEST_SQLSERVER_CONNECTION_STRING";

    private readonly TestWebApplicationFactory _baseFactory;

    public JobApplicationsSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task Create_GecerliBasvuruOlusurVeAuditKaydeder()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var jobPostingId = await CreatePublishedJobPostingAsync(setupClient, factory, runId);

        var candidateId = $"kan49-apply-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var client = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(client, candidateId);

        var response = await ApplyAsync(client, candidateId, jobPostingId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var application = await context.JobApplications
            .SingleOrDefaultAsync(
                a => a.JobPostingId == jobPostingId && a.CandidateProfileId == profileId);
        Assert.NotNull(application);

        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityCreated &&
                    l.TargetEntityType == ActivityEntityTypes.Application &&
                    l.TargetEntityId == application.Id.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(log);
        Assert.Equal(candidateId, log.CandidateId);
        Assert.Equal(jobPostingId.ToString(), log.JobPostingId);
    }

    [SqlServerIntegrationFact]
    public async Task Create_AyniAdayAyniIlanaIkinciKezBasvuramaz()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var jobPostingId = await CreatePublishedJobPostingAsync(setupClient, factory, runId);

        var candidateId = $"kan49-dup-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var client = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(client, candidateId);

        await ApplyAsync(client, candidateId, jobPostingId);
        var secondResponse = await ApplyAsync(client, candidateId, jobPostingId);

        Assert.Equal(HttpStatusCode.Redirect, secondResponse.StatusCode);

        await using var context = CreateRawContext();
        var count = await context.JobApplications
            .CountAsync(a => a.JobPostingId == jobPostingId && a.CandidateProfileId == profileId);
        Assert.Equal(1, count);
    }

    [SqlServerIntegrationFact]
    public async Task Create_KapaliIlanaBasvurulamaz()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var jobPostingId = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        await ChangeJobPostingStatusAsync(
            setupClient,
            jobPostingId,
            JobPostingStatuses.ApplicationsClosed);

        var candidateId = $"kan49-closed-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var client = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(client, candidateId);

        await ApplyAsync(client, candidateId, jobPostingId);

        await using var context = CreateRawContext();
        var count = await context.JobApplications
            .CountAsync(a => a.JobPostingId == jobPostingId && a.CandidateProfileId == profileId);
        Assert.Equal(0, count);
    }

    [SqlServerIntegrationFact]
    public async Task Create_SuresiGecmisIlanaBasvurulamaz()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var jobPostingId = await CreatePublishedJobPostingAsync(setupClient, factory, runId);

        await using (var context = CreateRawContext())
        {
            var pastDeadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE JobPostings SET ApplicationDeadline = {pastDeadline} WHERE Id = {jobPostingId}");
        }

        var candidateId = $"kan49-expired-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var client = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(client, candidateId);

        await ApplyAsync(client, candidateId, jobPostingId);

        await using var verificationContext = CreateRawContext();
        var count = await verificationContext.JobApplications
            .CountAsync(a => a.JobPostingId == jobPostingId && a.CandidateProfileId == profileId);
        Assert.Equal(0, count);
    }

    [SqlServerIntegrationFact]
    public async Task Create_EszamanliCiftIstekTekBasvuruUretir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var jobPostingId = await CreatePublishedJobPostingAsync(setupClient, factory, runId);

        var candidateId = $"kan49-race-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var profileClient = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(profileClient, candidateId);

        using var firstClient = CreateClient(factory);
        using var secondClient = CreateClient(factory);

        await Task.WhenAll(
            ApplyAsync(firstClient, candidateId, jobPostingId),
            ApplyAsync(secondClient, candidateId, jobPostingId));

        await using var context = CreateRawContext();
        var count = await context.JobApplications
            .CountAsync(a => a.JobPostingId == jobPostingId && a.CandidateProfileId == profileId);
        Assert.Equal(1, count);
    }

    [SqlServerIntegrationFact]
    public async Task Withdraw_YeniAsamadaGeriCekilirVeAuditKaydeder()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var jobPostingId = await CreatePublishedJobPostingAsync(setupClient, factory, runId);

        var candidateId = $"kan50-withdraw-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var client = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(client, candidateId);
        await ApplyAsync(client, candidateId, jobPostingId);

        await using var context = CreateRawContext();
        var applicationId = (await context.JobApplications
            .SingleAsync(a => a.CandidateProfileId == profileId)).Id;

        var response = await WithdrawAsync(client, candidateId, applicationId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var verificationContext = CreateRawContext();
        var application = await verificationContext.JobApplications.SingleAsync(a => a.Id == applicationId);
        Assert.Equal(ApplicationStatuses.Withdrawn, application.Status);
        Assert.NotNull(application.WithdrawnAtUtc);

        var log = await verificationContext.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityStatusChanged &&
                    l.TargetEntityType == ActivityEntityTypes.Application &&
                    l.TargetEntityId == applicationId.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(log);
        Assert.Equal(candidateId, log.CandidateId);
    }

    [SqlServerIntegrationFact]
    public async Task Withdraw_MulakatAsamasindaGeriCekilebilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var jobPostingId = await CreatePublishedJobPostingAsync(setupClient, factory, runId);

        var candidateId = $"kan50-interview-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var client = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(client, candidateId);
        await ApplyAsync(client, candidateId, jobPostingId);

        await using var context = CreateRawContext();
        var applicationId = (await context.JobApplications
            .SingleAsync(a => a.CandidateProfileId == profileId)).Id;
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE JobApplications SET Status = {ApplicationStatuses.Interview} WHERE Id = {applicationId}");

        var response = await WithdrawAsync(client, candidateId, applicationId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var verificationContext = CreateRawContext();
        var application = await verificationContext.JobApplications.SingleAsync(a => a.Id == applicationId);
        Assert.Equal(ApplicationStatuses.Withdrawn, application.Status);
    }

    [SqlServerIntegrationFact]
    public async Task Withdraw_AdayinKendiEylemiBildirimUretmez()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var jobPostingId = await CreatePublishedJobPostingAsync(setupClient, factory, runId);

        var candidateId = $"kan52-withdraw-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var client = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(client, candidateId);
        await ApplyAsync(client, candidateId, jobPostingId);

        await using var context = CreateRawContext();
        var applicationId = (await context.JobApplications
            .SingleAsync(a => a.CandidateProfileId == profileId)).Id;

        var response = await WithdrawAsync(client, candidateId, applicationId);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var verificationContext = CreateRawContext();
        var notificationCount = await verificationContext.Notifications
            .CountAsync(n => n.RecipientUserId == candidateId);
        Assert.Equal(0, notificationCount);
    }

    [SqlServerIntegrationFact]
    public async Task Withdraw_DurumGecmisiKaydedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var jobPostingId = await CreatePublishedJobPostingAsync(setupClient, factory, runId);

        var candidateId = $"kan24-history-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var client = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(client, candidateId);
        await ApplyAsync(client, candidateId, jobPostingId);

        await using var context = CreateRawContext();
        var applicationId = (await context.JobApplications
            .SingleAsync(a => a.CandidateProfileId == profileId)).Id;

        var response = await WithdrawAsync(client, candidateId, applicationId);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var verificationContext = CreateRawContext();
        var change = await verificationContext.JobApplicationStatusChanges
            .SingleAsync(c => c.JobApplicationId == applicationId);
        Assert.Equal(ApplicationStatuses.New, change.FromStatus);
        Assert.Equal(ApplicationStatuses.Withdrawn, change.ToStatus);
        Assert.Equal(candidateId, change.ActorUserId);
        Assert.Null(change.Reason);
    }

    [SqlServerIntegrationFact]
    public async Task Withdraw_EszamanliCiftIstekTekGecmisKaydiUretir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var jobPostingId = await CreatePublishedJobPostingAsync(setupClient, factory, runId);

        var candidateId = $"kan24-concurrency-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var profileClient = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(profileClient, candidateId);
        await ApplyAsync(profileClient, candidateId, jobPostingId);

        await using var context = CreateRawContext();
        var applicationId = (await context.JobApplications
            .SingleAsync(a => a.CandidateProfileId == profileId)).Id;

        using var firstClient = CreateClient(factory);
        using var secondClient = CreateClient(factory);

        await Task.WhenAll(
            WithdrawAsync(firstClient, candidateId, applicationId),
            WithdrawAsync(secondClient, candidateId, applicationId));

        await using var verificationContext = CreateRawContext();
        var application = await verificationContext.JobApplications.SingleAsync(a => a.Id == applicationId);
        Assert.Equal(ApplicationStatuses.Withdrawn, application.Status);

        var historyCount = await verificationContext.JobApplicationStatusChanges
            .CountAsync(c => c.JobApplicationId == applicationId);
        Assert.Equal(1, historyCount);
    }

    [SqlServerIntegrationFact]
    public async Task Withdraw_GeriCekilmisBasvuruTekrarGeriCekilemez()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var jobPostingId = await CreatePublishedJobPostingAsync(setupClient, factory, runId);

        var candidateId = $"kan50-twice-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var client = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(client, candidateId);
        await ApplyAsync(client, candidateId, jobPostingId);

        await using var context = CreateRawContext();
        var applicationId = (await context.JobApplications
            .SingleAsync(a => a.CandidateProfileId == profileId)).Id;

        await WithdrawAsync(client, candidateId, applicationId);

        await using var firstCheckContext = CreateRawContext();
        var firstWithdrawnAt = (await firstCheckContext.JobApplications
            .SingleAsync(a => a.Id == applicationId)).WithdrawnAtUtc;

        var secondResponse = await WithdrawAsync(client, candidateId, applicationId);

        Assert.Equal(HttpStatusCode.Redirect, secondResponse.StatusCode);

        await using var verificationContext = CreateRawContext();
        var application = await verificationContext.JobApplications.SingleAsync(a => a.Id == applicationId);
        Assert.Equal(ApplicationStatuses.Withdrawn, application.Status);
        Assert.Equal(firstWithdrawnAt, application.WithdrawnAtUtc);
    }

    [SqlServerIntegrationFact]
    public async Task Withdraw_BaskaAdayinBasvurusunuGeriCekemezNotFoundDoner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var jobPostingId = await CreatePublishedJobPostingAsync(setupClient, factory, runId);

        var candidateAId = $"kan50-owner-{runId}";
        var candidateBId = $"kan50-intruder-{runId}";
        await CreateCandidateUserAsync(candidateAId);
        await CreateCandidateUserAsync(candidateBId);

        using var aClient = CreateClient(factory);
        var profileAId = await CreateCandidateProfileAsync(aClient, candidateAId);
        await ApplyAsync(aClient, candidateAId, jobPostingId);

        using var bClient = CreateClient(factory);
        await CreateCandidateProfileAsync(bClient, candidateBId);

        await using var context = CreateRawContext();
        var applicationId = (await context.JobApplications
            .SingleAsync(a => a.CandidateProfileId == profileAId)).Id;

        var response = await WithdrawAsync(bClient, candidateBId, applicationId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var verificationContext = CreateRawContext();
        var application = await verificationContext.JobApplications.SingleAsync(a => a.Id == applicationId);
        Assert.Equal(ApplicationStatuses.New, application.Status);
        Assert.Null(application.WithdrawnAtUtc);
    }

    [SqlServerIntegrationFact]
    public async Task AcceptOffer_OnayliTeklifiKabulEderBasvuruyuIseAlindiYapar()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, recruiterId) =
            await CreatePublishedJobPostingWithRecruiterAsync(setupClient, factory, runId);

        var candidateId = $"kan63-accept-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var candidateClient = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(candidateClient, candidateId);
        await ApplyAsync(candidateClient, candidateId, jobPostingId);

        await using var context = CreateRawContext();
        var applicationId = (await context.JobApplications
            .SingleAsync(a => a.CandidateProfileId == profileId)).Id;
        await SetApplicationStatusAsync(applicationId, ApplicationStatuses.Interview);

        var managerId = await CreateHiringManagerUserAsync(factory, $"kan63-manager-{runId}", departmentId);
        using var recruiterClient = CreateClient(factory);
        await CreateOfferDraftAsync(recruiterClient, recruiterId, applicationId, 70000m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)));

        await using var offerContext = CreateRawContext();
        var offerId = (await offerContext.Offers.SingleAsync(o => o.JobApplicationId == applicationId)).Id;
        await SubmitOfferAsync(recruiterClient, recruiterId, offerId);

        using var managerClient = CreateClient(factory);
        await ApproveOfferAsync(managerClient, managerId, offerId);

        var response = await AcceptOfferAsync(candidateClient, candidateId, applicationId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var verifyContext = CreateRawContext();
        var offer = await verifyContext.Offers.SingleAsync(o => o.Id == offerId);
        Assert.Equal(OfferStatuses.Accepted, offer.Status);

        var application = await verifyContext.JobApplications.SingleAsync(a => a.Id == applicationId);
        Assert.Equal(ApplicationStatuses.Hired, application.Status);
    }

    [SqlServerIntegrationFact]
    public async Task AcceptOffer_OnayliOlmayanTeklifIcinBasarisizOlur()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, recruiterId) =
            await CreatePublishedJobPostingWithRecruiterAsync(setupClient, factory, runId);

        var candidateId = $"kan63-pending-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var candidateClient = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(candidateClient, candidateId);
        await ApplyAsync(candidateClient, candidateId, jobPostingId);

        await using var context = CreateRawContext();
        var applicationId = (await context.JobApplications
            .SingleAsync(a => a.CandidateProfileId == profileId)).Id;
        await SetApplicationStatusAsync(applicationId, ApplicationStatuses.Interview);

        using var recruiterClient = CreateClient(factory);
        await CreateOfferDraftAsync(recruiterClient, recruiterId, applicationId, 70000m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)));

        await using var offerContext = CreateRawContext();
        var offerId = (await offerContext.Offers.SingleAsync(o => o.JobApplicationId == applicationId)).Id;
        await SubmitOfferAsync(recruiterClient, recruiterId, offerId);

        var response = await AcceptOfferAsync(candidateClient, candidateId, applicationId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var verifyContext = CreateRawContext();
        var offer = await verifyContext.Offers.SingleAsync(o => o.Id == offerId);
        Assert.Equal(OfferStatuses.PendingManagerApproval, offer.Status);

        var application = await verifyContext.JobApplications.SingleAsync(a => a.Id == applicationId);
        Assert.Equal(ApplicationStatuses.Interview, application.Status);
    }

    [SqlServerIntegrationFact]
    public async Task RejectOffer_OnayliTeklifiReddederBasvuruyuReddedilirYapar()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, recruiterId) =
            await CreatePublishedJobPostingWithRecruiterAsync(setupClient, factory, runId);

        var candidateId = $"kan63-reject-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var candidateClient = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(candidateClient, candidateId);
        await ApplyAsync(candidateClient, candidateId, jobPostingId);

        await using var context = CreateRawContext();
        var applicationId = (await context.JobApplications
            .SingleAsync(a => a.CandidateProfileId == profileId)).Id;
        await SetApplicationStatusAsync(applicationId, ApplicationStatuses.Interview);

        var managerId = await CreateHiringManagerUserAsync(factory, $"kan63-manager-{runId}", departmentId);
        using var recruiterClient = CreateClient(factory);
        await CreateOfferDraftAsync(recruiterClient, recruiterId, applicationId, 70000m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)));

        await using var offerContext = CreateRawContext();
        var offerId = (await offerContext.Offers.SingleAsync(o => o.JobApplicationId == applicationId)).Id;
        await SubmitOfferAsync(recruiterClient, recruiterId, offerId);

        using var managerClient = CreateClient(factory);
        await ApproveOfferAsync(managerClient, managerId, offerId);

        var response = await RejectOfferAsync(candidateClient, candidateId, applicationId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var verifyContext = CreateRawContext();
        var offer = await verifyContext.Offers.SingleAsync(o => o.Id == offerId);
        Assert.Equal(OfferStatuses.RejectedByCandidate, offer.Status);

        var application = await verifyContext.JobApplications.SingleAsync(a => a.Id == applicationId);
        Assert.Equal(ApplicationStatuses.Rejected, application.Status);
    }

    [SqlServerIntegrationFact]
    public async Task AcceptOffer_BaskaAdayinBasvurusunaErisemezNotFoundDoner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, recruiterId) =
            await CreatePublishedJobPostingWithRecruiterAsync(setupClient, factory, runId);

        var ownerCandidateId = $"kan63-owner-{runId}";
        var intruderCandidateId = $"kan63-intruder-{runId}";
        await CreateCandidateUserAsync(ownerCandidateId);
        await CreateCandidateUserAsync(intruderCandidateId);

        using var ownerClient = CreateClient(factory);
        var ownerProfileId = await CreateCandidateProfileAsync(ownerClient, ownerCandidateId);
        await ApplyAsync(ownerClient, ownerCandidateId, jobPostingId);

        using var intruderClient = CreateClient(factory);
        await CreateCandidateProfileAsync(intruderClient, intruderCandidateId);

        await using var context = CreateRawContext();
        var applicationId = (await context.JobApplications
            .SingleAsync(a => a.CandidateProfileId == ownerProfileId)).Id;
        await SetApplicationStatusAsync(applicationId, ApplicationStatuses.Interview);

        var managerId = await CreateHiringManagerUserAsync(factory, $"kan63-manager2-{runId}", departmentId);
        using var recruiterClient = CreateClient(factory);
        await CreateOfferDraftAsync(recruiterClient, recruiterId, applicationId, 70000m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)));

        await using var offerContext = CreateRawContext();
        var offerId = (await offerContext.Offers.SingleAsync(o => o.JobApplicationId == applicationId)).Id;
        await SubmitOfferAsync(recruiterClient, recruiterId, offerId);

        using var managerClient = CreateClient(factory);
        await ApproveOfferAsync(managerClient, managerId, offerId);

        var response = await AcceptOfferAsync(intruderClient, intruderCandidateId, applicationId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<(int JobPostingId, int DepartmentId, string RecruiterId)>
        CreatePublishedJobPostingWithRecruiterAsync(
            HttpClient client,
            WebApplicationFactory<Program> factory,
            string runId)
    {
        var departmentName = $"Kan63-Dept-{runId}";
        var departmentToken = await GetAntiforgeryTokenForRoleAsync(client, "/Departments/Create", SystemRoles.Admin);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/Departments/Create"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Name"] = departmentName,
                    ["__RequestVerificationToken"] = departmentToken
                });
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        await using var context = CreateRawContext();
        var departmentId = (await context.Departments.SingleAsync(d => d.Name == departmentName)).Id;

        var positionName = $"Kan63-Pos-{runId}";
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

        var positionId = (await context.Positions.SingleAsync(p => p.Name == positionName)).Id;
        var recruiterId = await CreateRecruiterUserAsync(factory, $"kan63-recruiter-{runId}", departmentId);

        var jobPostingTitle = $"Kan63-Ilan-{runId}";
        var deadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var jobPostingToken = await GetAntiforgeryTokenForRoleAsync(client, "/JobPostings/Create", SystemRoles.Admin);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/JobPostings/Create"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Title"] = jobPostingTitle,
                    ["Description"] = "Kan-63 entegrasyon testi ilanı.",
                    ["PositionId"] = positionId.ToString(),
                    ["ResponsibleUserId"] = recruiterId,
                    ["ApplicationDeadline"] = deadline.ToString("yyyy-MM-dd"),
                    ["__RequestVerificationToken"] = jobPostingToken
                });
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var jobPostingId = (await context.JobPostings.SingleAsync(j => j.Title == jobPostingTitle)).Id;
        await ChangeJobPostingStatusAsync(client, jobPostingId, JobPostingStatuses.Published);

        return (jobPostingId, departmentId, recruiterId);
    }

    private static async Task SetApplicationStatusAsync(int applicationId, string status)
    {
        await using var context = CreateRawContext();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE JobApplications SET Status = {status} WHERE Id = {applicationId}");
    }

    private static async Task<HttpResponseMessage> CreateOfferDraftAsync(
        HttpClient client,
        string recruiterId,
        int applicationId,
        decimal salary,
        DateOnly startDate)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(
            client,
            $"/ApplicationsPool/Details/{applicationId}",
            SystemRoles.RecruitmentSpecialist,
            recruiterId);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/Offers/Create?applicationId={applicationId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, recruiterId);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Salary"] = salary.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["StartDate"] = startDate.ToString("yyyy-MM-dd"),
                ["__RequestVerificationToken"] = token
            });

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SubmitOfferAsync(
        HttpClient client,
        string recruiterId,
        int offerId)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(
            client,
            $"/Offers/Edit/{offerId}",
            SystemRoles.RecruitmentSpecialist,
            recruiterId);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Offers/Submit/{offerId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, recruiterId);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token });

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> ApproveOfferAsync(
        HttpClient client,
        string managerId,
        int offerId)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(
            client,
            $"/Offers/Edit/{offerId}",
            SystemRoles.HiringManager,
            managerId);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Offers/Approve/{offerId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.HiringManager);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, managerId);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token });

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> AcceptOfferAsync(
        HttpClient client,
        string candidateId,
        int applicationId)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(
            client,
            "/JobApplications",
            SystemRoles.Candidate,
            candidateId);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/JobApplications/AcceptOffer/{applicationId}");
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
        var token = await GetAntiforgeryTokenForRoleAsync(
            client,
            "/JobApplications",
            SystemRoles.Candidate,
            candidateId);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/JobApplications/RejectOffer/{applicationId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateId);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token });

        return await client.SendAsync(request);
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

    private static async Task<int> CreatePublishedJobPostingAsync(
        HttpClient client,
        WebApplicationFactory<Program> factory,
        string runId)
    {
        var departmentName = $"Kan49-Dept-{runId}";
        var departmentToken = await GetAntiforgeryTokenForRoleAsync(
            client,
            "/Departments/Create",
            SystemRoles.Admin);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/Departments/Create"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Name"] = departmentName,
                    ["__RequestVerificationToken"] = departmentToken
                });
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        await using var context = CreateRawContext();
        var departmentId = (await context.Departments
            .SingleAsync(d => d.Name == departmentName)).Id;

        var positionName = $"Kan49-Pos-{runId}";
        var positionToken = await GetAntiforgeryTokenForRoleAsync(
            client,
            "/Positions/Create",
            SystemRoles.Admin);
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

        var positionId = (await context.Positions.SingleAsync(p => p.Name == positionName)).Id;

        var recruiterUserName = $"kan49-recruiter-{runId}";
        var recruiterId = await CreateRecruiterUserAsync(factory, recruiterUserName, departmentId);

        var jobPostingTitle = $"Kan49-Ilan-{runId}";
        var deadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var jobPostingToken = await GetAntiforgeryTokenForRoleAsync(
            client,
            "/JobPostings/Create",
            SystemRoles.Admin);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/JobPostings/Create"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Title"] = jobPostingTitle,
                    ["Description"] = "Kan-49 entegrasyon testi ilanı.",
                    ["PositionId"] = positionId.ToString(),
                    ["ResponsibleUserId"] = recruiterId,
                    ["ApplicationDeadline"] = deadline.ToString("yyyy-MM-dd"),
                    ["__RequestVerificationToken"] = jobPostingToken
                });
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var jobPostingId = (await context.JobPostings
            .SingleAsync(j => j.Title == jobPostingTitle)).Id;

        await ChangeJobPostingStatusAsync(client, jobPostingId, JobPostingStatuses.Published);

        return jobPostingId;
    }

    private static async Task ChangeJobPostingStatusAsync(
        HttpClient client,
        int jobPostingId,
        string newStatus)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(
            client,
            $"/JobPostings/Details/{jobPostingId}",
            SystemRoles.Admin);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/JobPostings/ChangeStatus");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["id"] = jobPostingId.ToString(),
                ["newStatus"] = newStatus,
                ["__RequestVerificationToken"] = token
            });

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
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

    private static async Task<HttpResponseMessage> WithdrawAsync(
        HttpClient client,
        string candidateId,
        int applicationId)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(
            client,
            "/JobApplications",
            SystemRoles.Candidate,
            candidateId);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/JobApplications/Withdraw/{applicationId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateId);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token });

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
                    "geçici SQL Server iş başvurusu entegrasyon testi atlandı.";
            }
        }
    }
}
