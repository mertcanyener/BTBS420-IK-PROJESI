using System.Net;
using System.Text.RegularExpressions;
using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class JobPostingSqlServerIntegrationTests :
    IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN35_TEST_SQLSERVER_CONNECTION_STRING";

    private readonly TestWebApplicationFactory _baseFactory;

    public JobPostingSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task JobPosting_GecerliBilgilerleTaslakOlusurVeAuditKaydeder()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan41-Dept-{runId}");
        var positionId = await CreatePositionAsync(factory, departmentId, $"Kan41-Pos-{runId}");
        var recruiterId = await CreateRecruiterAsync(factory, departmentId, runId);
        var title = $"Kan41-JP-{runId}";
        using var client = CreateClient(factory);

        var response = await PostAsync(
            client,
            "/JobPostings/Create",
            new Dictionary<string, string>
            {
                ["Title"] = title,
                ["Description"] = "İlan açıklaması",
                ["PositionId"] = positionId.ToString(),
                ["ResponsibleUserId"] = recruiterId,
                ["ApplicationDeadline"] = FutureDeadline(30)
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var jobPosting = await context.JobPostings.SingleOrDefaultAsync(j => j.Title == title);
        Assert.NotNull(jobPosting);
        Assert.Equal(JobPostingStatuses.Draft, jobPosting.Status);
        Assert.Equal(positionId, jobPosting.PositionId);
        Assert.Equal(recruiterId, jobPosting.ResponsibleUserId);

        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityCreated &&
                    l.TargetEntityType == ActivityEntityTypes.JobPosting &&
                    l.TargetEntityId == jobPosting.Id.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(log);
    }

    [SqlServerIntegrationFact]
    public async Task JobPosting_GecmisSonBasvuruTarihiReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan41-Dept-{runId}");
        var positionId = await CreatePositionAsync(factory, departmentId, $"Kan41-Pos-{runId}");
        var recruiterId = await CreateRecruiterAsync(factory, departmentId, runId);
        var title = $"Kan41-JP-Past-{runId}";
        using var client = CreateClient(factory);

        var response = await PostAsync(
            client,
            "/JobPostings/Create",
            new Dictionary<string, string>
            {
                ["Title"] = title,
                ["Description"] = "İlan açıklaması",
                ["PositionId"] = positionId.ToString(),
                ["ResponsibleUserId"] = recruiterId,
                ["ApplicationDeadline"] = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
            });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ileri bir tarih", body);

        await using var context = CreateRawContext();
        var exists = await context.JobPostings.AnyAsync(j => j.Title == title);
        Assert.False(exists);
    }

    [SqlServerIntegrationFact]
    public async Task JobPosting_PasifPozisyonaBaglanamaz()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan41-InactiveDept-{runId}");
        var positionId = await CreatePositionAsync(factory, departmentId, $"Kan41-InactivePos-{runId}");
        await DeactivatePositionAsync(factory, positionId);
        var recruiterId = await CreateRecruiterAsync(factory, departmentId, runId);
        var title = $"Kan41-JP-Inactive-{runId}";
        using var client = CreateClient(factory);

        var response = await PostAsync(
            client,
            "/JobPostings/Create",
            new Dictionary<string, string>
            {
                ["Title"] = title,
                ["Description"] = "İlan açıklaması",
                ["PositionId"] = positionId.ToString(),
                ["ResponsibleUserId"] = recruiterId,
                ["ApplicationDeadline"] = FutureDeadline(30)
            });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("aktif de", body);

        await using var context = CreateRawContext();
        var exists = await context.JobPostings.AnyAsync(j => j.Title == title);
        Assert.False(exists);
    }

    [SqlServerIntegrationFact]
    public async Task JobPosting_EszamanliDuzenlemeCakismasiSessizceEzmez()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan41-ConcDept-{runId}");
        var positionId = await CreatePositionAsync(factory, departmentId, $"Kan41-ConcPos-{runId}");
        var recruiterId = await CreateRecruiterAsync(factory, departmentId, runId);
        var title = $"Kan41-JP-Conc-{runId}";
        using var createClient = CreateClient(factory);

        await PostAsync(
            createClient,
            "/JobPostings/Create",
            new Dictionary<string, string>
            {
                ["Title"] = title,
                ["Description"] = "İlan açıklaması",
                ["PositionId"] = positionId.ToString(),
                ["ResponsibleUserId"] = recruiterId,
                ["ApplicationDeadline"] = FutureDeadline(30)
            });

        int jobPostingId;
        await using (var context = CreateRawContext())
        {
            jobPostingId = (await context.JobPostings.SingleAsync(j => j.Title == title)).Id;
        }

        using var firstEditorClient = CreateClient(factory);
        using var secondEditorClient = CreateClient(factory);

        var firstForm = await GetEditFormStateAsync(firstEditorClient, jobPostingId);
        var secondForm = await GetEditFormStateAsync(secondEditorClient, jobPostingId);

        var firstUpdateTitle = $"{title}-First";
        var firstResponse = await PostAsync(
            firstEditorClient,
            $"/JobPostings/Edit/{jobPostingId}",
            new Dictionary<string, string>
            {
                ["Title"] = firstUpdateTitle,
                ["Description"] = "İlan açıklaması",
                ["PositionId"] = positionId.ToString(),
                ["ResponsibleUserId"] = recruiterId,
                ["ApplicationDeadline"] = FutureDeadline(30),
                ["RowVersion"] = firstForm.RowVersion
            },
            firstForm.AntiforgeryToken);
        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);

        var secondUpdateTitle = $"{title}-Second";
        var secondResponse = await PostAsync(
            secondEditorClient,
            $"/JobPostings/Edit/{jobPostingId}",
            new Dictionary<string, string>
            {
                ["Title"] = secondUpdateTitle,
                ["Description"] = "İlan açıklaması",
                ["PositionId"] = positionId.ToString(),
                ["ResponsibleUserId"] = recruiterId,
                ["ApplicationDeadline"] = FutureDeadline(30),
                ["RowVersion"] = secondForm.RowVersion
            },
            secondForm.AntiforgeryToken);
        var secondBody = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Contains("kontrol edip tekrar kaydedin", secondBody);

        await using var finalContext = CreateRawContext();
        var jobPosting = await finalContext.JobPostings.SingleAsync(j => j.Id == jobPostingId);
        Assert.Equal(firstUpdateTitle, jobPosting.Title);
    }

    [SqlServerIntegrationFact]
    public async Task Index_UzmanSadeceKendiSorumluOlduguIlanlariGorur()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan42-Dept-{runId}");
        var positionId = await CreatePositionAsync(factory, departmentId, $"Kan42-Pos-{runId}");
        var ownRecruiterId = await CreateRecruiterAsync(factory, departmentId, $"own-{runId}");
        var otherRecruiterId = await CreateRecruiterAsync(factory, departmentId, $"other-{runId}");
        var ownTitle = $"Kan42-Own-{runId}";
        var otherTitle = $"Kan42-Other-{runId}";
        await CreateJobPostingAsync(factory, positionId, ownRecruiterId, ownTitle);
        await CreateJobPostingAsync(factory, positionId, otherRecruiterId, otherTitle);

        var body = await GetIndexBodyAsync(
            factory,
            SystemRoles.RecruitmentSpecialist,
            ownRecruiterId);

        Assert.Contains(ownTitle, body);
        Assert.DoesNotContain(otherTitle, body);
    }

    [SqlServerIntegrationFact]
    public async Task Index_YoneticiSadeceKendiDepartmanindakiIlanlariGorur()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var ownDepartmentId = await CreateDepartmentAsync(factory, $"Kan42-OwnDept-{runId}");
        var otherDepartmentId = await CreateDepartmentAsync(factory, $"Kan42-OtherDept-{runId}");
        var ownPositionId = await CreatePositionAsync(factory, ownDepartmentId, $"Kan42-OwnPos-{runId}");
        var otherPositionId = await CreatePositionAsync(
            factory,
            otherDepartmentId,
            $"Kan42-OtherPos-{runId}");
        var recruiterId = await CreateRecruiterAsync(factory, ownDepartmentId, runId);
        var managerId = await CreateHiringManagerAsync(factory, ownDepartmentId, runId);
        var ownTitle = $"Kan42-OwnDeptJp-{runId}";
        var otherTitle = $"Kan42-OtherDeptJp-{runId}";
        await CreateJobPostingAsync(factory, ownPositionId, recruiterId, ownTitle);
        await CreateJobPostingAsync(factory, otherPositionId, recruiterId, otherTitle);

        var body = await GetIndexBodyAsync(factory, SystemRoles.HiringManager, managerId);

        Assert.Contains(ownTitle, body);
        Assert.DoesNotContain(otherTitle, body);
    }

    [SqlServerIntegrationFact]
    public async Task Index_SonTarihAraligiFiltresiDogruSatirlariDondurur()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan42-DeadlineDept-{runId}");
        var positionId = await CreatePositionAsync(factory, departmentId, $"Kan42-DeadlinePos-{runId}");
        var recruiterId = await CreateRecruiterAsync(factory, departmentId, runId);
        var nearTitle = $"Kan42-Near-{runId}";
        var farTitle = $"Kan42-Far-{runId}";
        await CreateJobPostingAsync(factory, positionId, recruiterId, nearTitle, deadlineDays: 10);
        await CreateJobPostingAsync(factory, positionId, recruiterId, farTitle, deadlineDays: 90);

        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)).ToString("yyyy-MM-dd");
        var to = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(120)).ToString("yyyy-MM-dd");
        var body = await GetIndexBodyAsync(
            factory,
            SystemRoles.Admin,
            TestAuthenticationHandler.DefaultUserId,
            $"?deadlineFrom={from}&deadlineTo={to}");

        Assert.Contains(farTitle, body);
        Assert.DoesNotContain(nearTitle, body);
    }

    [SqlServerIntegrationFact]
    public async Task Details_KapsamDisiIlanaErisim404Doner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan42-DetailDept-{runId}");
        var positionId = await CreatePositionAsync(factory, departmentId, $"Kan42-DetailPos-{runId}");
        var ownRecruiterId = await CreateRecruiterAsync(factory, departmentId, $"detail-own-{runId}");
        var otherRecruiterId = await CreateRecruiterAsync(factory, departmentId, $"detail-other-{runId}");
        var otherTitle = $"Kan42-DetailOther-{runId}";
        var jobPostingId = await CreateJobPostingAsync(factory, positionId, otherRecruiterId, otherTitle);

        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/JobPostings/Details/{jobPostingId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.RecruitmentSpecialist);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, ownRecruiterId);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task JobPosting_GecerliDurumGecisiYayinlarVeAuditKaydeder()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan43-Dept-{runId}");
        var positionId = await CreatePositionAsync(factory, departmentId, $"Kan43-Pos-{runId}");
        var recruiterId = await CreateRecruiterAsync(factory, departmentId, runId);
        var title = $"Kan43-JP-{runId}";
        var jobPostingId = await CreateJobPostingAsync(factory, positionId, recruiterId, title);

        using var client = CreateClient(factory);
        var response = await PostChangeStatusAsync(
            client,
            jobPostingId,
            JobPostingStatuses.Published,
            SystemRoles.Admin,
            TestAuthenticationHandler.DefaultUserId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var jobPosting = await context.JobPostings.SingleAsync(j => j.Id == jobPostingId);
        Assert.Equal(JobPostingStatuses.Published, jobPosting.Status);

        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityStatusChanged &&
                    l.TargetEntityType == ActivityEntityTypes.JobPosting &&
                    l.TargetEntityId == jobPosting.Id.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(log);
    }

    [SqlServerIntegrationFact]
    public async Task JobPosting_TanimsizDurumGecisiReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan43-InvalidDept-{runId}");
        var positionId = await CreatePositionAsync(factory, departmentId, $"Kan43-InvalidPos-{runId}");
        var recruiterId = await CreateRecruiterAsync(factory, departmentId, runId);
        var title = $"Kan43-JP-Invalid-{runId}";
        var jobPostingId = await CreateJobPostingAsync(factory, positionId, recruiterId, title);

        using var client = CreateClient(factory);
        var response = await PostChangeStatusAsync(
            client,
            jobPostingId,
            JobPostingStatuses.ApplicationsClosed,
            SystemRoles.Admin,
            TestAuthenticationHandler.DefaultUserId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var context = CreateRawContext();
        var jobPosting = await context.JobPostings.SingleAsync(j => j.Id == jobPostingId);
        Assert.Equal(JobPostingStatuses.Draft, jobPosting.Status);
    }

    [SqlServerIntegrationFact]
    public async Task JobPosting_SorumluOlmayanUzmanDurumDegistiremez()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan43-ScopeDept-{runId}");
        var positionId = await CreatePositionAsync(factory, departmentId, $"Kan43-ScopePos-{runId}");
        var ownerRecruiterId = await CreateRecruiterAsync(factory, departmentId, $"owner-{runId}");
        var otherRecruiterId = await CreateRecruiterAsync(factory, departmentId, $"other-{runId}");
        var title = $"Kan43-JP-Scope-{runId}";
        var jobPostingId = await CreateJobPostingAsync(factory, positionId, ownerRecruiterId, title);

        using var client = CreateClient(factory);
        var response = await PostChangeStatusAsync(
            client,
            jobPostingId,
            JobPostingStatuses.Published,
            SystemRoles.RecruitmentSpecialist,
            otherRecruiterId);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);

        await using var context = CreateRawContext();
        var jobPosting = await context.JobPostings.SingleAsync(j => j.Id == jobPostingId);
        Assert.Equal(JobPostingStatuses.Draft, jobPosting.Status);
    }

    [SqlServerIntegrationFact]
    public async Task JobPosting_YoneticiDurumDegistiremez()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan43-ManagerDept-{runId}");
        var positionId = await CreatePositionAsync(factory, departmentId, $"Kan43-ManagerPos-{runId}");
        var recruiterId = await CreateRecruiterAsync(factory, departmentId, runId);
        var managerId = await CreateHiringManagerAsync(factory, departmentId, runId);
        var title = $"Kan43-JP-Manager-{runId}";
        var jobPostingId = await CreateJobPostingAsync(factory, positionId, recruiterId, title);

        using var client = CreateClient(factory);
        var response = await PostChangeStatusAsync(
            client,
            jobPostingId,
            JobPostingStatuses.Published,
            SystemRoles.HiringManager,
            managerId);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);

        await using var context = CreateRawContext();
        var jobPosting = await context.JobPostings.SingleAsync(j => j.Id == jobPostingId);
        Assert.Equal(JobPostingStatuses.Draft, jobPosting.Status);
    }

    [SqlServerIntegrationFact]
    public async Task JobPosting_TaslakIlanFizikselOlarakSilinir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan44-DeleteDept-{runId}");
        var positionId = await CreatePositionAsync(factory, departmentId, $"Kan44-DeletePos-{runId}");
        var recruiterId = await CreateRecruiterAsync(factory, departmentId, runId);
        var title = $"Kan44-JP-Delete-{runId}";
        var jobPostingId = await CreateJobPostingAsync(factory, positionId, recruiterId, title);

        using var client = CreateClient(factory);
        var response = await PostDeleteAsync(
            client,
            jobPostingId,
            SystemRoles.Admin,
            TestAuthenticationHandler.DefaultUserId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var exists = await context.JobPostings.AnyAsync(j => j.Id == jobPostingId);
        Assert.False(exists);

        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityDeleted &&
                    l.TargetEntityType == ActivityEntityTypes.JobPosting &&
                    l.TargetEntityId == jobPostingId.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(log);
    }

    [SqlServerIntegrationFact]
    public async Task JobPosting_YayinlanmisIlanSilinemez()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan44-NoDeleteDept-{runId}");
        var positionId = await CreatePositionAsync(factory, departmentId, $"Kan44-NoDeletePos-{runId}");
        var recruiterId = await CreateRecruiterAsync(factory, departmentId, runId);
        var title = $"Kan44-JP-NoDelete-{runId}";
        var jobPostingId = await CreateJobPostingAsync(factory, positionId, recruiterId, title);

        using var client = CreateClient(factory);
        await PostChangeStatusAsync(
            client,
            jobPostingId,
            JobPostingStatuses.Published,
            SystemRoles.Admin,
            TestAuthenticationHandler.DefaultUserId);

        var response = await PostDeleteAsync(
            client,
            jobPostingId,
            SystemRoles.Admin,
            TestAuthenticationHandler.DefaultUserId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var context = CreateRawContext();
        var exists = await context.JobPostings.AnyAsync(j => j.Id == jobPostingId);
        Assert.True(exists);
    }

    [SqlServerIntegrationFact]
    public async Task JobPosting_ArsivlenmisIlandanBaskaDurumaGecisYapilamaz()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan44-ArchiveDept-{runId}");
        var positionId = await CreatePositionAsync(factory, departmentId, $"Kan44-ArchivePos-{runId}");
        var recruiterId = await CreateRecruiterAsync(factory, departmentId, runId);
        var title = $"Kan44-JP-Archive-{runId}";
        var jobPostingId = await CreateJobPostingAsync(factory, positionId, recruiterId, title);

        using var client = CreateClient(factory);
        await PostChangeStatusAsync(
            client,
            jobPostingId,
            JobPostingStatuses.Published,
            SystemRoles.Admin,
            TestAuthenticationHandler.DefaultUserId);
        var archiveResponse = await PostChangeStatusAsync(
            client,
            jobPostingId,
            JobPostingStatuses.Archived,
            SystemRoles.Admin,
            TestAuthenticationHandler.DefaultUserId);

        Assert.Equal(HttpStatusCode.Redirect, archiveResponse.StatusCode);

        await using (var context = CreateRawContext())
        {
            var jobPosting = await context.JobPostings.SingleAsync(j => j.Id == jobPostingId);
            Assert.Equal(JobPostingStatuses.Archived, jobPosting.Status);

            var log = await context.ActivityLogs
                .Where(
                    l =>
                        l.ActionCode == ActivityActionCodes.EntityArchived &&
                        l.TargetEntityType == ActivityEntityTypes.JobPosting &&
                        l.TargetEntityId == jobPostingId.ToString())
                .FirstOrDefaultAsync();
            Assert.NotNull(log);
        }

        var reopenResponse = await PostChangeStatusAsync(
            client,
            jobPostingId,
            JobPostingStatuses.Published,
            SystemRoles.Admin,
            TestAuthenticationHandler.DefaultUserId);

        Assert.Equal(HttpStatusCode.BadRequest, reopenResponse.StatusCode);

        await using var finalContext = CreateRawContext();
        var finalJobPosting = await finalContext.JobPostings.SingleAsync(j => j.Id == jobPostingId);
        Assert.Equal(JobPostingStatuses.Archived, finalJobPosting.Status);
    }

    [SqlServerIntegrationFact]
    public async Task PublicJobPostings_SadeceYayindaVeDisaAcikIlanlarGorunur()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan48-VisDept-{runId}");
        var positionId = await CreatePositionAsync(factory, departmentId, $"Kan48-VisPos-{runId}");
        var recruiterId = await CreateRecruiterAsync(factory, departmentId, runId);

        var publishedTitle = $"Kan48-Published-{runId}";
        var publishedId = await CreateJobPostingAsync(factory, positionId, recruiterId, publishedTitle);
        using (var client = CreateClient(factory))
        {
            await PostChangeStatusAsync(
                client,
                publishedId,
                JobPostingStatuses.Published,
                SystemRoles.Admin,
                TestAuthenticationHandler.DefaultUserId);
        }

        var draftTitle = $"Kan48-Draft-{runId}";
        await CreateJobPostingAsync(factory, positionId, recruiterId, draftTitle);

        var internalTitle = $"Kan48-Internal-{runId}";
        var internalId = await CreateJobPostingAsync(
            factory,
            positionId,
            recruiterId,
            internalTitle,
            isInternal: true);
        using (var client = CreateClient(factory))
        {
            await PostChangeStatusAsync(
                client,
                internalId,
                JobPostingStatuses.Published,
                SystemRoles.Admin,
                TestAuthenticationHandler.DefaultUserId);
        }

        var body = await GetPublicIndexBodyAsync(factory);

        Assert.Contains(publishedTitle, body);
        Assert.DoesNotContain(draftTitle, body);
        Assert.DoesNotContain(internalTitle, body);
    }

    [SqlServerIntegrationFact]
    public async Task PublicJobPostings_PozisyonFiltresiCalisir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan48-FilterDept-{runId}");
        var matchingPositionId = await CreatePositionAsync(
            factory,
            departmentId,
            $"Kan48-MatchPos-{runId}");
        var otherPositionId = await CreatePositionAsync(
            factory,
            departmentId,
            $"Kan48-OtherPos-{runId}");
        var recruiterId = await CreateRecruiterAsync(factory, departmentId, runId);

        var matchingTitle = $"Kan48-Match-{runId}";
        var matchingId = await CreateJobPostingAsync(factory, matchingPositionId, recruiterId, matchingTitle);
        var otherTitle = $"Kan48-Other-{runId}";
        var otherId = await CreateJobPostingAsync(factory, otherPositionId, recruiterId, otherTitle);

        using (var client = CreateClient(factory))
        {
            await PostChangeStatusAsync(
                client,
                matchingId,
                JobPostingStatuses.Published,
                SystemRoles.Admin,
                TestAuthenticationHandler.DefaultUserId);
            await PostChangeStatusAsync(
                client,
                otherId,
                JobPostingStatuses.Published,
                SystemRoles.Admin,
                TestAuthenticationHandler.DefaultUserId);
        }

        var body = await GetPublicIndexBodyAsync(factory, $"?positionId={matchingPositionId}");

        Assert.Contains(matchingTitle, body);
        Assert.DoesNotContain(otherTitle, body);
    }

    [SqlServerIntegrationFact]
    public async Task PublicJobPostings_SayfalamaDogruCalisir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan48-PageDept-{runId}");
        var positionId = await CreatePositionAsync(factory, departmentId, $"Kan48-PagePos-{runId}");
        var recruiterId = await CreateRecruiterAsync(factory, departmentId, runId);

        var firstTitle = $"Kan48-Page1-{runId}";
        var secondTitle = $"Kan48-Page2-{runId}";
        var firstId = await CreateJobPostingAsync(
            factory,
            positionId,
            recruiterId,
            firstTitle,
            deadlineDays: 10);
        var secondId = await CreateJobPostingAsync(
            factory,
            positionId,
            recruiterId,
            secondTitle,
            deadlineDays: 20);

        using (var client = CreateClient(factory))
        {
            await PostChangeStatusAsync(
                client,
                firstId,
                JobPostingStatuses.Published,
                SystemRoles.Admin,
                TestAuthenticationHandler.DefaultUserId);
            await PostChangeStatusAsync(
                client,
                secondId,
                JobPostingStatuses.Published,
                SystemRoles.Admin,
                TestAuthenticationHandler.DefaultUserId);
        }

        var firstPageBody = await GetPublicIndexBodyAsync(factory, $"?positionId={positionId}&page=1&pageSize=1");
        Assert.Contains(firstTitle, firstPageBody);
        Assert.DoesNotContain(secondTitle, firstPageBody);

        var secondPageBody = await GetPublicIndexBodyAsync(factory, $"?positionId={positionId}&page=2&pageSize=1");
        Assert.Contains(secondTitle, secondPageBody);
        Assert.DoesNotContain(firstTitle, secondPageBody);
    }

    [SqlServerIntegrationFact]
    public async Task PublicJobPostings_TaslakVeyaIcIlanDetayi404Doner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan48-DetailDept-{runId}");
        var positionId = await CreatePositionAsync(factory, departmentId, $"Kan48-DetailPos-{runId}");
        var recruiterId = await CreateRecruiterAsync(factory, departmentId, runId);
        var draftTitle = $"Kan48-DraftDetail-{runId}";
        var draftId = await CreateJobPostingAsync(factory, positionId, recruiterId, draftTitle);

        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/PublicJobPostings/Details/{draftId}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task PublicJobPostings_YayindaVeDisaAcikIlanDetayGosterir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var departmentId = await CreateDepartmentAsync(factory, $"Kan48-ShowDept-{runId}");
        var positionId = await CreatePositionAsync(factory, departmentId, $"Kan48-ShowPos-{runId}");
        var recruiterId = await CreateRecruiterAsync(factory, departmentId, runId);
        var title = $"Kan48-ShowDetail-{runId}";
        var jobPostingId = await CreateJobPostingAsync(factory, positionId, recruiterId, title);

        using var client = CreateClient(factory);
        await PostChangeStatusAsync(
            client,
            jobPostingId,
            JobPostingStatuses.Published,
            SystemRoles.Admin,
            TestAuthenticationHandler.DefaultUserId);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/PublicJobPostings/Details/{jobPostingId}");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(title, body);
    }

    private static async Task<string> GetPublicIndexBodyAsync(
        WebApplicationFactory<Program> factory,
        string queryString = "")
    {
        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/PublicJobPostings{queryString}");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<HttpResponseMessage> PostDeleteAsync(
        HttpClient client,
        int jobPostingId,
        string role,
        string userId)
    {
        string token;
        using (var tokenRequest = new HttpRequestMessage(HttpMethod.Get, "/JobPostings/Create"))
        {
            tokenRequest.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
            tokenRequest.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, userId);
            var tokenResponse = await client.SendAsync(tokenRequest);
            var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
            var tokenMatch = Regex.Match(
                tokenContent,
                "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
                RegexOptions.CultureInvariant);
            token = tokenMatch.Success ? WebUtility.HtmlDecode(tokenMatch.Groups[1].Value) : string.Empty;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/JobPostings/Delete");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, userId);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["id"] = jobPostingId.ToString(),
                ["__RequestVerificationToken"] = token
            });

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PostChangeStatusAsync(
        HttpClient client,
        int jobPostingId,
        string newStatus,
        string role,
        string userId)
    {
        string token;
        using (var tokenRequest = new HttpRequestMessage(HttpMethod.Get, "/JobPostings/Create"))
        {
            tokenRequest.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
            tokenRequest.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, userId);
            var tokenResponse = await client.SendAsync(tokenRequest);
            var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
            var tokenMatch = Regex.Match(
                tokenContent,
                "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
                RegexOptions.CultureInvariant);
            token = tokenMatch.Success ? WebUtility.HtmlDecode(tokenMatch.Groups[1].Value) : string.Empty;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/JobPostings/ChangeStatus");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, userId);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["id"] = jobPostingId.ToString(),
                ["newStatus"] = newStatus,
                ["__RequestVerificationToken"] = token
            });

        return await client.SendAsync(request);
    }

    private static async Task<int> CreateJobPostingAsync(
        WebApplicationFactory<Program> factory,
        int positionId,
        string responsibleUserId,
        string title,
        int deadlineDays = 30,
        bool isInternal = false)
    {
        using var client = CreateClient(factory);
        var response = await PostAsync(
            client,
            "/JobPostings/Create",
            new Dictionary<string, string>
            {
                ["Title"] = title,
                ["Description"] = "İlan açıklaması",
                ["PositionId"] = positionId.ToString(),
                ["ResponsibleUserId"] = responsibleUserId,
                ["ApplicationDeadline"] = FutureDeadline(deadlineDays),
                ["IsInternal"] = isInternal.ToString()
            });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var jobPosting = await context.JobPostings.SingleAsync(j => j.Title == title);

        return jobPosting.Id;
    }

    private static async Task<string> CreateHiringManagerAsync(
        WebApplicationFactory<Program> factory,
        int departmentId,
        string runId)
    {
        using var client = CreateClient(factory);
        var userName = $"kan42-manager-{runId}";
        var response = await PostAsync(
            client,
            "/Users/Create",
            new Dictionary<string, string>
            {
                ["UserName"] = userName,
                ["Email"] = $"{userName}@example.test",
                ["DepartmentId"] = departmentId.ToString(),
                ["Role"] = SystemRoles.HiringManager
            });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var user = await context.Users.SingleAsync(u => u.UserName == userName);

        return user.Id;
    }

    private static async Task<string> GetIndexBodyAsync(
        WebApplicationFactory<Program> factory,
        string role,
        string userId,
        string queryString = "")
    {
        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/JobPostings{queryString}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, userId);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadAsStringAsync();
    }

    private static string FutureDeadline(int daysFromNow)
    {
        return DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysFromNow)).ToString("yyyy-MM-dd");
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

    private static async Task<int> CreateDepartmentAsync(
        WebApplicationFactory<Program> factory,
        string name)
    {
        using var client = CreateClient(factory);
        var response = await PostAsync(
            client,
            "/Departments/Create",
            new Dictionary<string, string> { ["Name"] = name });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var department = await context.Departments.SingleAsync(d => d.Name == name);

        return department.Id;
    }

    private static async Task<int> CreatePositionAsync(
        WebApplicationFactory<Program> factory,
        int departmentId,
        string name)
    {
        using var client = CreateClient(factory);
        var response = await PostAsync(
            client,
            "/Positions/Create",
            new Dictionary<string, string>
            {
                ["Name"] = name,
                ["DepartmentId"] = departmentId.ToString()
            });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var position = await context.Positions.SingleAsync(p => p.Name == name);

        return position.Id;
    }

    private static async Task DeactivatePositionAsync(
        WebApplicationFactory<Program> factory,
        int positionId)
    {
        using var client = CreateClient(factory);
        var response = await PostAsync(client, $"/Positions/Deactivate/{positionId}", []);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<string> CreateRecruiterAsync(
        WebApplicationFactory<Program> factory,
        int departmentId,
        string runId)
    {
        using var client = CreateClient(factory);
        var userName = $"kan41-recruiter-{runId}";
        var response = await PostAsync(
            client,
            "/Users/Create",
            new Dictionary<string, string>
            {
                ["UserName"] = userName,
                ["Email"] = $"{userName}@example.test",
                ["DepartmentId"] = departmentId.ToString(),
                ["Role"] = SystemRoles.RecruitmentSpecialist
            });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var user = await context.Users.SingleAsync(u => u.UserName == userName);

        return user.Id;
    }

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string path,
        Dictionary<string, string> formFields,
        string? antiforgeryToken = null)
    {
        var token = antiforgeryToken ?? await GetAntiforgeryTokenAsync(client, GetFormUrl(path));
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
        formFields["__RequestVerificationToken"] = token;
        request.Content = new FormUrlEncodedContent(formFields);

        return await client.SendAsync(request);
    }

    private static string GetFormUrl(string postPath)
    {
        if (postPath.Contains("/Create") || postPath.Contains("/Edit/"))
        {
            return postPath;
        }

        var controllerSegment = postPath.Split('/', StringSplitOptions.RemoveEmptyEntries)[0];

        return $"/{controllerSegment}";
    }

    private static async Task<(string AntiforgeryToken, string RowVersion)> GetEditFormStateAsync(
        HttpClient client,
        int jobPostingId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/JobPostings/Edit/{jobPostingId}");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);

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
        Assert.True(rowVersionMatch.Success, "RowVersion gizli alanı bulunamadı.");

        return (
            WebUtility.HtmlDecode(tokenMatch.Groups[1].Value),
            WebUtility.HtmlDecode(rowVersionMatch.Groups[1].Value));
    }

    private static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        var tokenMatch = Regex.Match(
            content,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(
            tokenMatch.Success,
            $"Antiforgery form alanı bulunamadı ({url}).");

        return WebUtility.HtmlDecode(tokenMatch.Groups[1].Value);
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
                    "geçici SQL Server ilan entegrasyon testi atlandı.";
            }
        }
    }
}
