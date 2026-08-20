using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.ApplicationsPool;
using BTBS420.RecruitmentSystem.Web.ViewModels.Dashboard;
using BTBS420.RecruitmentSystem.Web.ViewModels.Interviews;
using BTBS420.RecruitmentSystem.Web.ViewModels.Positions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.HiringManagerOnly)]
public sealed class ManagerDashboardController(
    ApplicationDbContext dbContext,
    IRecruitmentScopeService scopeService) : Controller
{
    private const int MaxItemsPerSection = 10;

    private static readonly string[] ShortlistStatuses =
    [
        ApplicationStatuses.Screening,
        ApplicationStatuses.Interview
    ];

    [HttpGet]
    public async Task<IActionResult> Index(
        int? positionId,
        int? jobPostingId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        CancellationToken cancellationToken)
    {
        var scope = await scopeService.GetScopeAsync(User, cancellationToken);
        if (scope is null)
        {
            return Forbid();
        }

        var metrics = await BuildMetricsAsync(scope, positionId, jobPostingId, dateFrom, dateTo, cancellationToken);
        var shortlist = await BuildShortlistAsync(
            scope, positionId, jobPostingId, dateFrom, dateTo, cancellationToken);
        var pendingEvaluations = await BuildPendingEvaluationsAsync(
            scope, positionId, jobPostingId, dateFrom, dateTo, cancellationToken);

        var filter = new ManagerDashboardFilterViewModel(positionId, jobPostingId, dateFrom, dateTo);
        var filterOptions = await BuildFilterOptionsAsync(scope, cancellationToken);

        return View(
            new ManagerDashboardViewModel(
                metrics, shortlist, pendingEvaluations, filter, filterOptions));
    }

    private async Task<ManagerDashboardMetricsViewModel> BuildMetricsAsync(
        RecruitmentScope scope,
        int? positionId,
        int? jobPostingId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        CancellationToken cancellationToken)
    {
        var jobPostingQuery = ApplyJobPostingFilters(
            scope.ApplyToJobPostings(dbContext.JobPostings), positionId, jobPostingId);
        var applicationQuery = ApplyJobApplicationFilters(
            scope.ApplyToJobApplications(dbContext.JobApplications),
            positionId,
            jobPostingId,
            dateFrom,
            dateTo);

        return new ManagerDashboardMetricsViewModel(
            await jobPostingQuery
                .Where(jobPosting => jobPosting.Status == JobPostingStatuses.Published)
                .CountAsync(cancellationToken),
            await applicationQuery
                .Where(application => application.Status == ApplicationStatuses.New)
                .CountAsync(cancellationToken),
            await applicationQuery
                .Where(application => application.Status == ApplicationStatuses.Screening)
                .CountAsync(cancellationToken),
            await applicationQuery
                .Where(application => application.Status == ApplicationStatuses.Interview)
                .CountAsync(cancellationToken),
            await applicationQuery
                .Where(application => application.Status == ApplicationStatuses.Hired)
                .CountAsync(cancellationToken),
            await applicationQuery
                .Where(application => application.Status == ApplicationStatuses.Rejected)
                .CountAsync(cancellationToken),
            await applicationQuery
                .Where(application => application.Status == ApplicationStatuses.Withdrawn)
                .CountAsync(cancellationToken));
    }

    private async Task<IReadOnlyList<ApplicationPoolListItemViewModel>> BuildShortlistAsync(
        RecruitmentScope scope,
        int? positionId,
        int? jobPostingId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        CancellationToken cancellationToken)
    {
        var query = ApplyJobApplicationFilters(
            scope.ApplyToJobApplications(dbContext.JobApplications),
            positionId,
            jobPostingId,
            dateFrom,
            dateTo);
        query = query.Where(application => ShortlistStatuses.Contains(application.Status));

        return await query
            .OrderByDescending(application => application.AppliedAtUtc)
            .Take(MaxItemsPerSection)
            .Select(
                application => new ApplicationPoolListItemViewModel(
                    application.Id,
                    application.CandidateProfile.FirstName + " " + application.CandidateProfile.LastName,
                    application.JobPosting.Title,
                    application.JobPosting.Position.Name,
                    application.JobPosting.Position.Department.Name,
                    ApplicationStatuses.GetDisplayLabel(application.Status),
                    application.AppliedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<PendingEvaluationListItemViewModel>> BuildPendingEvaluationsAsync(
        RecruitmentScope scope,
        int? positionId,
        int? jobPostingId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        CancellationToken cancellationToken)
    {
        var query = scope.ApplyToInterviews(dbContext.Interviews)
            .Where(interview => interview.Status == InterviewStatuses.Completed);

        if (positionId.HasValue)
        {
            query = query.Where(
                interview => interview.JobApplication.JobPosting.PositionId == positionId.Value);
        }

        if (jobPostingId.HasValue)
        {
            query = query.Where(
                interview => interview.JobApplication.JobPostingId == jobPostingId.Value);
        }

        if (dateFrom.HasValue)
        {
            var fromUtc = dateFrom.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(interview => interview.StartAtUtc >= fromUtc);
        }

        if (dateTo.HasValue)
        {
            var toUtc = dateTo.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(interview => interview.StartAtUtc <= toUtc);
        }

        query = query.Where(
            interview => dbContext.InterviewParticipants
                .Any(
                    participant =>
                        participant.InterviewId == interview.Id &&
                        !dbContext.InterviewEvaluations.Any(
                            evaluation =>
                                evaluation.InterviewId == interview.Id &&
                                evaluation.EvaluatorUserId == participant.ParticipantUserId)));

        var interviews = await query
            .OrderByDescending(interview => interview.StartAtUtc)
            .Take(MaxItemsPerSection)
            .Select(
                interview => new
                {
                    interview.Id,
                    CandidateFullName =
                        interview.JobApplication.CandidateProfile.FirstName + " " +
                        interview.JobApplication.CandidateProfile.LastName,
                    JobPostingTitle = interview.JobApplication.JobPosting.Title,
                    interview.StartAtUtc
                })
            .ToListAsync(cancellationToken);

        var interviewIds = interviews.Select(interview => interview.Id).ToList();

        var missingEvaluatorRows = await dbContext.InterviewParticipants
            .Where(participant => interviewIds.Contains(participant.InterviewId))
            .Where(
                participant => !dbContext.InterviewEvaluations.Any(
                    evaluation =>
                        evaluation.InterviewId == participant.InterviewId &&
                        evaluation.EvaluatorUserId == participant.ParticipantUserId))
            .Select(
                participant => new
                {
                    participant.InterviewId,
                    ParticipantName = participant.ParticipantUser.UserName
                })
            .ToListAsync(cancellationToken);

        var missingEvaluatorLookup = missingEvaluatorRows
            .GroupBy(row => row.InterviewId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(row => row.ParticipantName ?? string.Empty)
                    .ToList());

        return interviews
            .Select(
                interview => new PendingEvaluationListItemViewModel(
                    interview.Id,
                    interview.CandidateFullName,
                    interview.JobPostingTitle,
                    interview.StartAtUtc,
                    missingEvaluatorLookup.TryGetValue(interview.Id, out var missingNames)
                        ? missingNames
                        : []))
            .ToList();
    }

    private static IQueryable<JobPosting> ApplyJobPostingFilters(
        IQueryable<JobPosting> query, int? positionId, int? jobPostingId)
    {
        if (positionId.HasValue)
        {
            query = query.Where(jobPosting => jobPosting.PositionId == positionId.Value);
        }

        if (jobPostingId.HasValue)
        {
            query = query.Where(jobPosting => jobPosting.Id == jobPostingId.Value);
        }

        return query;
    }

    private static IQueryable<JobApplication> ApplyJobApplicationFilters(
        IQueryable<JobApplication> query,
        int? positionId,
        int? jobPostingId,
        DateOnly? dateFrom,
        DateOnly? dateTo)
    {
        if (positionId.HasValue)
        {
            query = query.Where(application => application.JobPosting.PositionId == positionId.Value);
        }

        if (jobPostingId.HasValue)
        {
            query = query.Where(application => application.JobPostingId == jobPostingId.Value);
        }

        if (dateFrom.HasValue)
        {
            var fromUtc = dateFrom.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(application => application.AppliedAtUtc >= fromUtc);
        }

        if (dateTo.HasValue)
        {
            var toUtc = dateTo.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(application => application.AppliedAtUtc <= toUtc);
        }

        return query;
    }

    private async Task<ManagerDashboardFilterOptionsViewModel> BuildFilterOptionsAsync(
        RecruitmentScope scope,
        CancellationToken cancellationToken)
    {
        var scopedJobPostings = scope.ApplyToJobPostings(dbContext.JobPostings);

        var positionOptions = await scopedJobPostings
            .Select(jobPosting => new { jobPosting.PositionId, jobPosting.Position.Name })
            .Distinct()
            .OrderBy(position => position.Name)
            .Select(position => new SelectOptionViewModel(position.PositionId, position.Name))
            .ToListAsync(cancellationToken);

        var jobPostingOptions = await scopedJobPostings
            .OrderBy(jobPosting => jobPosting.Title)
            .Select(jobPosting => new SelectOptionViewModel(jobPosting.Id, jobPosting.Title))
            .ToListAsync(cancellationToken);

        return new ManagerDashboardFilterOptionsViewModel(positionOptions, jobPostingOptions);
    }
}
