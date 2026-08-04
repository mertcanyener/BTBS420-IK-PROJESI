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

public sealed class ApplicationsPoolSqlServerIntegrationTests :
    IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN46_TEST_SQLSERVER_CONNECTION_STRING";

    private static readonly byte[] ValidPdfBytes =
        System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF");

    private readonly TestWebApplicationFactory _baseFactory;

    public ApplicationsPoolSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task Details_SorumluUzmanErisebilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);

        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        using var specialistClient = CreateClient(factory);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/ApplicationsPool/Details/{applicationId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, responsibleUserId);

        var response = await specialistClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task Details_KapsamDisindakiUzmanErisemezNotFoundDoner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, _) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var otherRecruiterId = await CreateRecruiterUserAsync(
            factory,
            $"kan51-other-recruiter-{runId}",
            departmentId);

        using var otherClient = CreateClient(factory);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/ApplicationsPool/Details/{applicationId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, otherRecruiterId);

        var response = await otherClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task Details_DogruDepartmandakiYoneticiErisebilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, _) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var managerId = await CreateHiringManagerUserAsync(
            factory,
            $"kan51-manager-{runId}",
            departmentId);

        using var managerClient = CreateClient(factory);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/ApplicationsPool/Details/{applicationId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.HiringManager);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, managerId);

        var response = await managerClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task Details_YanlisDepartmandakiYoneticiErisemezNotFoundDoner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, _) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var otherDepartmentId = await CreateDepartmentAsync(setupClient, $"Kan51-OtherDept-{runId}");
        var managerId = await CreateHiringManagerUserAsync(
            factory,
            $"kan51-wrongmanager-{runId}",
            otherDepartmentId);

        using var managerClient = CreateClient(factory);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/ApplicationsPool/Details/{applicationId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.HiringManager);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, managerId);

        var response = await managerClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task Details_AdminHerZamanErisebilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, _) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        using var adminClient = CreateClient(factory);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/ApplicationsPool/Details/{applicationId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);

        var response = await adminClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task AddNote_GecerliNotEklenirVeAktorZamanKaydedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        using var specialistClient = CreateClient(factory);
        var noteBody = $"Kan51-not-{runId}";
        var response = await AddNoteAsync(
            specialistClient,
            SystemRoles.RecruitmentSpecialist,
            responsibleUserId,
            applicationId,
            noteBody);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var note = await context.ApplicationNotes
            .SingleOrDefaultAsync(n => n.JobApplicationId == applicationId && n.Body == noteBody);
        Assert.NotNull(note);
        Assert.Equal(responsibleUserId, note.AuthorUserId);
    }

    [SqlServerIntegrationFact]
    public async Task AddNote_KapsamDisindakiUzmanNotEkleyemezNotFoundDoner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, _) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var otherRecruiterId = await CreateRecruiterUserAsync(
            factory,
            $"kan51-note-intruder-{runId}",
            departmentId);

        // Yetkisiz personelin geçerli bir antiforgery token'ı olmadan gönderdiği istek
        // zaten (antiforgery doğrulaması scope kontrolünden önce çalıştığı için) 500
        // ile reddedilir; kapsam kontrolünü izole test edebilmek için önce bu kullanıcının
        // erişebildiği kendi başvurusundan geçerli bir token alıyoruz.
        using var otherClient = CreateClient(factory);
        var (ownJobPostingId, _, _) = await CreatePublishedJobPostingAsync(
            otherClient,
            factory,
            $"{runId}-own",
            otherRecruiterId);
        var ownApplicationId = await CreateApplicationAsync(factory, otherClient, $"{runId}-own", ownJobPostingId);
        var token = await GetAntiforgeryTokenForRoleAsync(
            otherClient,
            $"/ApplicationsPool/Details/{ownApplicationId}",
            SystemRoles.RecruitmentSpecialist,
            otherRecruiterId);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/ApplicationsPool/AddNote/{applicationId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, otherRecruiterId);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["body"] = "yetkisiz not",
                ["__RequestVerificationToken"] = token
            });

        var response = await otherClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var context = CreateRawContext();
        var count = await context.ApplicationNotes.CountAsync(n => n.JobApplicationId == applicationId);
        Assert.Equal(0, count);
    }

    [SqlServerIntegrationFact]
    public async Task DownloadDocument_KapsamDahilindekiPersonelIndirebilirVeAuditKaydeder()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);

        var candidateId = $"kan51-doc-candidate-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var candidateClient = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(candidateClient, candidateId);
        await ApplyAsync(candidateClient, candidateId, jobPostingId);
        var documentId = await UploadDocumentAsync(candidateClient, candidateId);

        await using var context = CreateRawContext();
        var applicationId = (await context.JobApplications
            .SingleAsync(a => a.CandidateProfileId == profileId)).Id;

        using var specialistClient = CreateClient(factory);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/ApplicationsPool/DownloadDocument/{applicationId}?documentId={documentId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, responsibleUserId);

        var response = await specialistClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var downloadedBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(ValidPdfBytes, downloadedBytes);

        await using var verificationContext = CreateRawContext();
        var log = await verificationContext.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityDownloaded &&
                    l.TargetEntityType == ActivityEntityTypes.CandidateDocument &&
                    l.TargetEntityId == documentId.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(log);
        Assert.Equal(candidateId, log.CandidateId);
    }

    [SqlServerIntegrationFact]
    public async Task DownloadDocument_KapsamDisindakiPersonelIndiremezNotFoundDoner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, _) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);

        var candidateId = $"kan51-doc-scope-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var candidateClient = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(candidateClient, candidateId);
        await ApplyAsync(candidateClient, candidateId, jobPostingId);
        var documentId = await UploadDocumentAsync(candidateClient, candidateId);

        await using var context = CreateRawContext();
        var applicationId = (await context.JobApplications
            .SingleAsync(a => a.CandidateProfileId == profileId)).Id;

        var otherRecruiterId = await CreateRecruiterUserAsync(
            factory,
            $"kan51-doc-intruder-{runId}",
            departmentId);

        using var otherClient = CreateClient(factory);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/ApplicationsPool/DownloadDocument/{applicationId}?documentId={documentId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, otherRecruiterId);

        var response = await otherClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task CreateInterview_GecerliCevrimiciMulakatOlusurPlanlandiDurumunda()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        using var specialistClient = CreateClient(factory);
        var start = DateTime.UtcNow.AddDays(2);
        var end = start.AddHours(1);
        var response = await CreateInterviewAsync(
            specialistClient,
            responsibleUserId,
            applicationId,
            InterviewTypes.Online,
            start,
            end,
            onlineMeetingLink: "https://meet.example.test/kan54",
            location: null);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var interview = await context.Interviews.SingleAsync(i => i.JobApplicationId == applicationId);
        Assert.Equal(InterviewStatuses.Scheduled, interview.Status);
        Assert.Equal(InterviewTypes.Online, interview.InterviewType);
        Assert.Equal("https://meet.example.test/kan54", interview.OnlineMeetingLink);
        Assert.Null(interview.Location);
    }

    [SqlServerIntegrationFact]
    public async Task CreateInterview_GecerliYuzYuzeMulakatOlusur()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        using var specialistClient = CreateClient(factory);
        var start = DateTime.UtcNow.AddDays(2);
        var end = start.AddHours(1);
        var response = await CreateInterviewAsync(
            specialistClient,
            responsibleUserId,
            applicationId,
            InterviewTypes.InPerson,
            start,
            end,
            onlineMeetingLink: null,
            location: "Merkez Ofis - Toplantı Odası 3");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var interview = await context.Interviews.SingleAsync(i => i.JobApplicationId == applicationId);
        Assert.Equal(InterviewStatuses.Scheduled, interview.Status);
        Assert.Equal("Merkez Ofis - Toplantı Odası 3", interview.Location);
        Assert.Null(interview.OnlineMeetingLink);
    }

    [SqlServerIntegrationFact]
    public async Task CreateInterview_BaslangicBitistenSonraysaReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        using var specialistClient = CreateClient(factory);
        var start = DateTime.UtcNow.AddDays(2);
        var end = start.AddHours(-1);
        var response = await CreateInterviewAsync(
            specialistClient,
            responsibleUserId,
            applicationId,
            InterviewTypes.Online,
            start,
            end,
            onlineMeetingLink: "https://meet.example.test/kan54",
            location: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = CreateRawContext();
        var count = await context.Interviews.CountAsync(i => i.JobApplicationId == applicationId);
        Assert.Equal(0, count);
    }

    [SqlServerIntegrationFact]
    public async Task CreateInterview_CevrimiciTurdeLinkEksikseReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        using var specialistClient = CreateClient(factory);
        var start = DateTime.UtcNow.AddDays(2);
        var end = start.AddHours(1);
        var response = await CreateInterviewAsync(
            specialistClient,
            responsibleUserId,
            applicationId,
            InterviewTypes.Online,
            start,
            end,
            onlineMeetingLink: null,
            location: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = CreateRawContext();
        var count = await context.Interviews.CountAsync(i => i.JobApplicationId == applicationId);
        Assert.Equal(0, count);
    }

    [SqlServerIntegrationFact]
    public async Task CreateInterview_YuzYuzeTurdeKonumEksikseReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        using var specialistClient = CreateClient(factory);
        var start = DateTime.UtcNow.AddDays(2);
        var end = start.AddHours(1);
        var response = await CreateInterviewAsync(
            specialistClient,
            responsibleUserId,
            applicationId,
            InterviewTypes.InPerson,
            start,
            end,
            onlineMeetingLink: null,
            location: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = CreateRawContext();
        var count = await context.Interviews.CountAsync(i => i.JobApplicationId == applicationId);
        Assert.Equal(0, count);
    }

    [SqlServerIntegrationFact]
    public async Task Reject_GecerliGecisBasariliVeAuditKaydeder()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        await using var setupContext = CreateRawContext();
        await setupContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE JobApplications SET Status = {ApplicationStatuses.Screening} WHERE Id = {applicationId}");

        using var specialistClient = CreateClient(factory);
        var reason = $"Kan53-red-{runId}";
        var response = await RejectAsync(
            specialistClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId, reason);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var application = await context.JobApplications
            .Include(a => a.CandidateProfile)
            .Include(a => a.JobPosting)
            .SingleAsync(a => a.Id == applicationId);
        Assert.Equal(ApplicationStatuses.Rejected, application.Status);

        var change = await context.JobApplicationStatusChanges
            .SingleAsync(c => c.JobApplicationId == applicationId);
        Assert.Equal(ApplicationStatuses.Screening, change.FromStatus);
        Assert.Equal(ApplicationStatuses.Rejected, change.ToStatus);
        Assert.Equal(reason, change.Reason);
        Assert.Equal(responsibleUserId, change.ActorUserId);

        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityStatusChanged &&
                    l.TargetEntityType == ActivityEntityTypes.Application &&
                    l.TargetEntityId == applicationId.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(log);

        var candidateUserId = application.CandidateProfile.ApplicationUserId;
        var notification = await context.Notifications
            .SingleOrDefaultAsync(n => n.RecipientUserId == candidateUserId);
        Assert.NotNull(notification);
        Assert.Contains(application.JobPosting.Title, notification.Message);
        Assert.Contains(ApplicationStatuses.GetDisplayLabel(ApplicationStatuses.Screening), notification.Message);
        Assert.Contains(ApplicationStatuses.GetDisplayLabel(ApplicationStatuses.Rejected), notification.Message);
    }

    [SqlServerIntegrationFact]
    public async Task Reject_YeniBasvuruDogrudanReddedilemez()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        using var specialistClient = CreateClient(factory);
        var response = await RejectAsync(
            specialistClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId, "Gerekçe");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var application = await context.JobApplications
            .Include(a => a.CandidateProfile)
            .SingleAsync(a => a.Id == applicationId);
        Assert.Equal(ApplicationStatuses.New, application.Status);

        var changeCount = await context.JobApplicationStatusChanges
            .CountAsync(c => c.JobApplicationId == applicationId);
        Assert.Equal(0, changeCount);

        var notificationCount = await context.Notifications
            .CountAsync(n => n.RecipientUserId == application.CandidateProfile.ApplicationUserId);
        Assert.Equal(0, notificationCount);
    }

    [SqlServerIntegrationFact]
    public async Task Reject_KapsamDisindakiUzmanReddedemezNotFoundDoner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, _) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        await using var setupContext = CreateRawContext();
        await setupContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE JobApplications SET Status = {ApplicationStatuses.Screening} WHERE Id = {applicationId}");

        var otherRecruiterId = await CreateRecruiterUserAsync(
            factory, $"kan53-reject-intruder-{runId}", departmentId);

        using var otherClient = CreateClient(factory);
        var (ownJobPostingId, _, _) = await CreatePublishedJobPostingAsync(
            otherClient, factory, $"{runId}-own", otherRecruiterId);
        var ownApplicationId = await CreateApplicationAsync(factory, otherClient, $"{runId}-own", ownJobPostingId);
        var token = await GetAntiforgeryTokenForRoleAsync(
            otherClient,
            $"/ApplicationsPool/Details/{ownApplicationId}",
            SystemRoles.RecruitmentSpecialist,
            otherRecruiterId);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/ApplicationsPool/Reject/{applicationId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, otherRecruiterId);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["reason"] = "Yetkisiz red",
                ["__RequestVerificationToken"] = token
            });

        var response = await otherClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var context = CreateRawContext();
        var application = await context.JobApplications.SingleAsync(a => a.Id == applicationId);
        Assert.Equal(ApplicationStatuses.Screening, application.Status);
    }

    [SqlServerIntegrationFact]
    public async Task Reevaluate_GecerliGecisBasariliVeAuditKaydeder()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        using var specialistClient = CreateClient(factory);
        await RejectAsync(specialistClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId, null);
        await using (var rejectContext = CreateRawContext())
        {
            await rejectContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE JobApplications SET Status = {ApplicationStatuses.Screening} WHERE Id = {applicationId}");
            await rejectContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE JobApplications SET Status = {ApplicationStatuses.Rejected} WHERE Id = {applicationId}");
        }

        var reason = $"Kan53-yeniden-{runId}";
        var response = await ReevaluateAsync(
            specialistClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId, reason);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var application = await context.JobApplications
            .Include(a => a.CandidateProfile)
            .Include(a => a.JobPosting)
            .SingleAsync(a => a.Id == applicationId);
        Assert.Equal(ApplicationStatuses.Screening, application.Status);

        var change = await context.JobApplicationStatusChanges
            .Where(c => c.JobApplicationId == applicationId && c.ToStatus == ApplicationStatuses.Screening)
            .SingleAsync();
        Assert.Equal(ApplicationStatuses.Rejected, change.FromStatus);
        Assert.Equal(reason, change.Reason);

        var notification = await context.Notifications
            .Where(n => n.RecipientUserId == application.CandidateProfile.ApplicationUserId)
            .OrderByDescending(n => n.Id)
            .FirstOrDefaultAsync();
        Assert.NotNull(notification);
        Assert.Contains(ApplicationStatuses.GetDisplayLabel(ApplicationStatuses.Rejected), notification.Message);
        Assert.Contains(ApplicationStatuses.GetDisplayLabel(ApplicationStatuses.Screening), notification.Message);
    }

    [SqlServerIntegrationFact]
    public async Task RejectVeReevaluate_HerBasariliGecisAyriBildirimUretir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        await using (var setupContext = CreateRawContext())
        {
            await setupContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE JobApplications SET Status = {ApplicationStatuses.Screening} WHERE Id = {applicationId}");
        }

        using var specialistClient = CreateClient(factory);
        await RejectAsync(specialistClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId, "İlk red");
        await ReevaluateAsync(specialistClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId, "Yeniden değerlendir");

        await using var context = CreateRawContext();
        var application = await context.JobApplications
            .Include(a => a.CandidateProfile)
            .SingleAsync(a => a.Id == applicationId);

        var notificationCount = await context.Notifications
            .CountAsync(n => n.RecipientUserId == application.CandidateProfile.ApplicationUserId);
        Assert.Equal(2, notificationCount);
    }

    [SqlServerIntegrationFact]
    public async Task Reevaluate_GerekcesizReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        await using var setupContext = CreateRawContext();
        await setupContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE JobApplications SET Status = {ApplicationStatuses.Rejected} WHERE Id = {applicationId}");

        using var specialistClient = CreateClient(factory);
        var response = await ReevaluateAsync(
            specialistClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId, string.Empty);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var application = await context.JobApplications.SingleAsync(a => a.Id == applicationId);
        Assert.Equal(ApplicationStatuses.Rejected, application.Status);
    }

    [SqlServerIntegrationFact]
    public async Task Archive_TerminalDurumdanBasariliVeAuditKaydeder()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        await using var setupContext = CreateRawContext();
        await setupContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE JobApplications SET Status = {ApplicationStatuses.Rejected} WHERE Id = {applicationId}");

        using var specialistClient = CreateClient(factory);
        var response = await ArchiveAsync(specialistClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var application = await context.JobApplications.SingleAsync(a => a.Id == applicationId);
        Assert.True(application.IsArchived);
        Assert.NotNull(application.ArchivedAtUtc);
        Assert.Equal(ApplicationStatuses.Rejected, application.Status);

        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityArchived &&
                    l.TargetEntityType == ActivityEntityTypes.Application &&
                    l.TargetEntityId == applicationId.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(log);
    }

    [SqlServerIntegrationFact]
    public async Task Archive_AktifDurumdanReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        using var specialistClient = CreateClient(factory);
        var response = await ArchiveAsync(specialistClient, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var application = await context.JobApplications.SingleAsync(a => a.Id == applicationId);
        Assert.False(application.IsArchived);
    }

    [SqlServerIntegrationFact]
    public async Task Reject_EszamanliIkiIstektenBiriKazanirTekGecmisKaydiUretir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, _, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        await using var setupContext = CreateRawContext();
        await setupContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE JobApplications SET Status = {ApplicationStatuses.Screening} WHERE Id = {applicationId}");

        using var client1 = CreateClient(factory);
        using var client2 = CreateClient(factory);
        var token1 = await GetAntiforgeryTokenForRoleAsync(
            client1, $"/ApplicationsPool/Details/{applicationId}", SystemRoles.RecruitmentSpecialist, responsibleUserId);
        var token2 = await GetAntiforgeryTokenForRoleAsync(
            client2, $"/ApplicationsPool/Details/{applicationId}", SystemRoles.RecruitmentSpecialist, responsibleUserId);

        var responses = await Task.WhenAll(
            RejectWithTokenAsync(client1, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId, "Birinci", token1),
            RejectWithTokenAsync(client2, SystemRoles.RecruitmentSpecialist, responsibleUserId, applicationId, "İkinci", token2));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Redirect, response.StatusCode));

        await using var context = CreateRawContext();
        var application = await context.JobApplications
            .Include(a => a.CandidateProfile)
            .SingleAsync(a => a.Id == applicationId);
        Assert.Equal(ApplicationStatuses.Rejected, application.Status);

        var winningChange = await context.JobApplicationStatusChanges
            .SingleAsync(c => c.JobApplicationId == applicationId);

        // Kaybeden istek DbUpdateConcurrencyException ile geri alındığı için ne bir
        // JobApplicationStatusChange kaydı ne de bir bildirim bırakmamalı: adaya yalnızca
        // kazanan geçişin EventKey'iyle eşleşen tek bir bildirim düşmeli.
        var expectedEventKey =
            $"application-status-changed:{applicationId}:{ApplicationStatuses.Rejected}:{winningChange.ChangedAtUtc.Ticks}";
        var notifications = await context.Notifications
            .Where(n => n.RecipientUserId == application.CandidateProfile.ApplicationUserId)
            .ToListAsync();
        Assert.Single(notifications);
        Assert.Equal(expectedEventKey, notifications[0].EventKey);
    }

    [SqlServerIntegrationFact]
    public async Task CreateInterview_KapsamDisindakiUzmanOlusturamazNotFoundDoner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, _) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        var otherRecruiterId = await CreateRecruiterUserAsync(
            factory,
            $"kan54-intruder-{runId}",
            departmentId);

        using var otherClient = CreateClient(factory);
        var start = DateTime.UtcNow.AddDays(2);
        var end = start.AddHours(1);
        var response = await CreateInterviewAsync(
            otherClient,
            otherRecruiterId,
            applicationId,
            InterviewTypes.Online,
            start,
            end,
            onlineMeetingLink: "https://meet.example.test/kan54",
            location: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var context = CreateRawContext();
        var count = await context.Interviews.CountAsync(i => i.JobApplicationId == applicationId);
        Assert.Equal(0, count);
    }

    [SqlServerIntegrationFact]
    public async Task AssignParticipants_GecerliAtamaBasariliVeAuditKaydeder()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        using var specialistClient = CreateClient(factory);
        var start = DateTime.UtcNow.AddDays(3);
        var end = start.AddHours(1);
        var interviewId = await CreateInterviewAndGetIdAsync(
            specialistClient,
            responsibleUserId,
            applicationId,
            InterviewTypes.Online,
            start,
            end,
            "https://meet.example.test/kan55",
            null);

        var participantId = await CreateRecruiterUserAsync(
            factory,
            $"kan55-participant-{runId}",
            departmentId);

        var response = await AssignParticipantsAsync(
            specialistClient,
            SystemRoles.RecruitmentSpecialist,
            responsibleUserId,
            applicationId,
            interviewId,
            [participantId]);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var participant = await context.InterviewParticipants
            .SingleOrDefaultAsync(p => p.InterviewId == interviewId && p.ParticipantUserId == participantId);
        Assert.NotNull(participant);

        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityUpdated &&
                    l.TargetEntityType == ActivityEntityTypes.Interview &&
                    l.TargetEntityId == interviewId.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(log);
    }

    [SqlServerIntegrationFact]
    public async Task AssignParticipants_OrtusenAktifMulakataAtanamaz()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        using var specialistClient = CreateClient(factory);
        var start1 = DateTime.UtcNow.AddDays(3);
        var end1 = start1.AddHours(1);
        var start2 = start1.AddMinutes(30);
        var end2 = start2.AddHours(1);

        var interview1Id = await CreateInterviewAndGetIdAsync(
            specialistClient, responsibleUserId, applicationId, InterviewTypes.Online, start1, end1,
            "https://meet.example.test/i1", null);
        var interview2Id = await CreateInterviewAndGetIdAsync(
            specialistClient, responsibleUserId, applicationId, InterviewTypes.Online, start2, end2,
            "https://meet.example.test/i2", null);

        var participantId = await CreateRecruiterUserAsync(
            factory,
            $"kan55-overlap-{runId}",
            departmentId);

        await AssignParticipantsAsync(
            specialistClient, SystemRoles.RecruitmentSpecialist, responsibleUserId,
            applicationId, interview1Id, [participantId]);
        await AssignParticipantsAsync(
            specialistClient, SystemRoles.RecruitmentSpecialist, responsibleUserId,
            applicationId, interview2Id, [participantId]);

        await using var context = CreateRawContext();
        var count = await context.InterviewParticipants.CountAsync(p => p.ParticipantUserId == participantId);
        Assert.Equal(1, count);
        var assignedInterviewId = await context.InterviewParticipants
            .Where(p => p.ParticipantUserId == participantId)
            .Select(p => p.InterviewId)
            .SingleAsync();
        Assert.Equal(interview1Id, assignedInterviewId);
    }

    [SqlServerIntegrationFact]
    public async Task AssignParticipants_ArdisikZamanAraliklariKabulEdilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        using var specialistClient = CreateClient(factory);
        var start1 = DateTime.UtcNow.AddDays(3);
        var end1 = start1.AddHours(1);
        var start2 = end1;
        var end2 = start2.AddHours(1);

        var interview1Id = await CreateInterviewAndGetIdAsync(
            specialistClient, responsibleUserId, applicationId, InterviewTypes.Online, start1, end1,
            "https://meet.example.test/i1", null);
        var interview2Id = await CreateInterviewAndGetIdAsync(
            specialistClient, responsibleUserId, applicationId, InterviewTypes.Online, start2, end2,
            "https://meet.example.test/i2", null);

        var participantId = await CreateRecruiterUserAsync(
            factory,
            $"kan55-contiguous-{runId}",
            departmentId);

        await AssignParticipantsAsync(
            specialistClient, SystemRoles.RecruitmentSpecialist, responsibleUserId,
            applicationId, interview1Id, [participantId]);
        await AssignParticipantsAsync(
            specialistClient, SystemRoles.RecruitmentSpecialist, responsibleUserId,
            applicationId, interview2Id, [participantId]);

        await using var context = CreateRawContext();
        var count = await context.InterviewParticipants.CountAsync(p => p.ParticipantUserId == participantId);
        Assert.Equal(2, count);
    }

    [SqlServerIntegrationFact]
    public async Task AssignParticipants_IptalEdilmisMulakatlaCakismaEngelSayilmaz()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        using var specialistClient = CreateClient(factory);
        var start1 = DateTime.UtcNow.AddDays(3);
        var end1 = start1.AddHours(1);
        var start2 = start1.AddMinutes(30);
        var end2 = start2.AddHours(1);

        var interview1Id = await CreateInterviewAndGetIdAsync(
            specialistClient, responsibleUserId, applicationId, InterviewTypes.Online, start1, end1,
            "https://meet.example.test/i1", null);
        var interview2Id = await CreateInterviewAndGetIdAsync(
            specialistClient, responsibleUserId, applicationId, InterviewTypes.Online, start2, end2,
            "https://meet.example.test/i2", null);

        var participantId = await CreateRecruiterUserAsync(
            factory,
            $"kan55-cancelled-{runId}",
            departmentId);

        await AssignParticipantsAsync(
            specialistClient, SystemRoles.RecruitmentSpecialist, responsibleUserId,
            applicationId, interview1Id, [participantId]);

        await using (var context = CreateRawContext())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Interviews SET Status = {InterviewStatuses.Cancelled} WHERE Id = {interview1Id}");
        }

        await AssignParticipantsAsync(
            specialistClient, SystemRoles.RecruitmentSpecialist, responsibleUserId,
            applicationId, interview2Id, [participantId]);

        await using var verificationContext = CreateRawContext();
        var count = await verificationContext.InterviewParticipants
            .CountAsync(p => p.ParticipantUserId == participantId);
        Assert.Equal(2, count);
    }

    [SqlServerIntegrationFact]
    public async Task AssignParticipants_EsZamanliCakisanAtamaTekBasariliOlur()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var setupClient = CreateClient(factory);
        var (jobPostingId, departmentId, responsibleUserId) = await CreatePublishedJobPostingAsync(setupClient, factory, runId);
        var applicationId = await CreateApplicationAsync(factory, setupClient, runId, jobPostingId);

        using var specialistClient = CreateClient(factory);
        var start1 = DateTime.UtcNow.AddDays(3);
        var end1 = start1.AddHours(1);
        var start2 = start1.AddMinutes(30);
        var end2 = start2.AddHours(1);

        var interview1Id = await CreateInterviewAndGetIdAsync(
            specialistClient, responsibleUserId, applicationId, InterviewTypes.Online, start1, end1,
            "https://meet.example.test/i1", null);
        var interview2Id = await CreateInterviewAndGetIdAsync(
            specialistClient, responsibleUserId, applicationId, InterviewTypes.Online, start2, end2,
            "https://meet.example.test/i2", null);

        var participantId = await CreateRecruiterUserAsync(
            factory,
            $"kan55-race-{runId}",
            departmentId);

        using var client1 = CreateClient(factory);
        using var client2 = CreateClient(factory);

        await Task.WhenAll(
            AssignParticipantsAsync(
                client1, SystemRoles.RecruitmentSpecialist, responsibleUserId,
                applicationId, interview1Id, [participantId]),
            AssignParticipantsAsync(
                client2, SystemRoles.RecruitmentSpecialist, responsibleUserId,
                applicationId, interview2Id, [participantId]));

        await using var context = CreateRawContext();
        var count = await context.InterviewParticipants.CountAsync(p => p.ParticipantUserId == participantId);
        Assert.Equal(1, count);
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
        var response = await CreateInterviewAsync(
            client, recruiterId, applicationId, interviewType, startAtUtc, endAtUtc,
            onlineMeetingLink, location);
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

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> CreateInterviewAsync(
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

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> RejectAsync(
        HttpClient client,
        string role,
        string userId,
        int applicationId,
        string? reason)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(
            client, $"/ApplicationsPool/Details/{applicationId}", role, userId);
        return await RejectWithTokenAsync(client, role, userId, applicationId, reason, token);
    }

    private static async Task<HttpResponseMessage> RejectWithTokenAsync(
        HttpClient client,
        string role,
        string userId,
        int applicationId,
        string? reason,
        string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/ApplicationsPool/Reject/{applicationId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, userId);

        var formFields = new Dictionary<string, string> { ["__RequestVerificationToken"] = token };
        if (reason is not null)
        {
            formFields["reason"] = reason;
        }

        request.Content = new FormUrlEncodedContent(formFields);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> ReevaluateAsync(
        HttpClient client,
        string role,
        string userId,
        int applicationId,
        string reason)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(
            client, $"/ApplicationsPool/Details/{applicationId}", role, userId);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/ApplicationsPool/Reevaluate/{applicationId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, userId);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["reason"] = reason,
                ["__RequestVerificationToken"] = token
            });

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> ArchiveAsync(
        HttpClient client,
        string role,
        string userId,
        int applicationId)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(
            client, $"/ApplicationsPool/Details/{applicationId}", role, userId);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/ApplicationsPool/Archive/{applicationId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, userId);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token });

        return await client.SendAsync(request);
    }

    private static async Task<int> CreateApplicationAsync(
        WebApplicationFactory<Program> factory,
        HttpClient setupClient,
        string runId,
        int jobPostingId)
    {
        var candidateId = $"kan51-candidate-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var candidateClient = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(candidateClient, candidateId);
        await ApplyAsync(candidateClient, candidateId, jobPostingId);

        await using var context = CreateRawContext();
        return (await context.JobApplications.SingleAsync(a => a.CandidateProfileId == profileId)).Id;
    }

    private static async Task<HttpResponseMessage> AddNoteAsync(
        HttpClient client,
        string role,
        string userId,
        int applicationId,
        string body)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(
            client,
            $"/ApplicationsPool/Details/{applicationId}",
            role,
            userId);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/ApplicationsPool/AddNote/{applicationId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, userId);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["body"] = body,
                ["__RequestVerificationToken"] = token
            });

        return await client.SendAsync(request);
    }

    private static async Task<int> UploadDocumentAsync(HttpClient client, string candidateId)
    {
        var token = await GetAntiforgeryTokenForRoleAsync(
            client,
            "/CandidateDocuments/Create",
            SystemRoles.Candidate,
            candidateId);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/CandidateDocuments/Create");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateId);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(CandidateDocumentTypes.Resume), "DocumentType");
        content.Add(new StringContent(token), "__RequestVerificationToken");

        var fileContent = new ByteArrayContent(ValidPdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "File", "ozgecmis.pdf");

        request.Content = content;
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var document = await context.CandidateDocuments
            .Where(d => d.CandidateProfile.ApplicationUserId == candidateId)
            .OrderByDescending(d => d.UploadedAtUtc)
            .FirstAsync();

        return document.Id;
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
        var departmentId = await CreateDepartmentAsync(client, $"Kan51-Dept-{runId}");

        var positionName = $"Kan51-Pos-{runId}";
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
            $"kan51-recruiter-{runId}",
            departmentId);

        var jobPostingTitle = $"Kan51-Ilan-{runId}";
        var deadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var jobPostingToken = await GetAntiforgeryTokenForRoleAsync(client, "/JobPostings/Create", SystemRoles.Admin);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/JobPostings/Create"))
        {
            request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Title"] = jobPostingTitle,
                    ["Description"] = "Kan-51 entegrasyon testi ilanı.",
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
                    "geçici SQL Server başvuru havuzu entegrasyon testi atlandı.";
            }
        }
    }
}
