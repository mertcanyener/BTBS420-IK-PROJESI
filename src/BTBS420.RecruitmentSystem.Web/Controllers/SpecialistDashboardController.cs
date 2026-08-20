using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.ApplicationsPool;
using BTBS420.RecruitmentSystem.Web.ViewModels.Dashboard;
using BTBS420.RecruitmentSystem.Web.ViewModels.Interviews;
using BTBS420.RecruitmentSystem.Web.ViewModels.JobPostings;
using BTBS420.RecruitmentSystem.Web.ViewModels.Offers;
using BTBS420.RecruitmentSystem.Web.ViewModels.Positions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.RecruitmentSpecialistOnly)]
public sealed class SpecialistDashboardController(
    ApplicationDbContext dbContext,
    IRecruitmentScopeService scopeService,
    TimeProvider timeProvider) : Controller
{
    private const int MaxItemsPerSection = 10;

    [HttpGet]
    public async Task<IActionResult> Index(
        string? status,
        int? departmentId,
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

        var jobPostings = await BuildJobPostingsAsync(
            scope, departmentId, positionId, jobPostingId, cancellationToken);
        var candidatePool = await BuildCandidatePoolAsync(
            scope, status, departmentId, positionId, jobPostingId, dateFrom, dateTo, cancellationToken);
        var upcomingInterviews = await BuildUpcomingInterviewsAsync(
            scope, departmentId, positionId, jobPostingId, dateFrom, dateTo, cancellationToken);
        var pendingOffers = await BuildPendingOffersAsync(
            scope, departmentId, positionId, jobPostingId, dateFrom, dateTo, cancellationToken);

        var filter = new SpecialistDashboardFilterViewModel(
            status, departmentId, positionId, jobPostingId, dateFrom, dateTo);
        var filterOptions = await BuildFilterOptionsAsync(scope, cancellationToken);

        return View(
            new SpecialistDashboardViewModel(
                jobPostings, candidatePool, upcomingInterviews, pendingOffers, filter, filterOptions));
    }

    private async Task<IReadOnlyList<JobPostingListItemViewModel>> BuildJobPostingsAsync(
        RecruitmentScope scope,
        int? departmentId,
        int? positionId,
        int? jobPostingId,
        CancellationToken cancellationToken)
    {
        var query = ApplyJobPostingFilters(
            scope.ApplyToJobPostings(dbContext.JobPostings), departmentId, positionId, jobPostingId);

        return await query
            .OrderBy(jobPosting => jobPosting.Title)
            .Take(MaxItemsPerSection)
            .Select(
                jobPosting => new JobPostingListItemViewModel(
                    jobPosting.Id,
                    jobPosting.Title,
                    jobPosting.Position.Name,
                    jobPosting.Position.Department.Name,
                    jobPosting.ResponsibleUser.UserName!,
                    jobPosting.ApplicationDeadline,
                    jobPosting.Status))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ApplicationPoolListItemViewModel>> BuildCandidatePoolAsync(
        RecruitmentScope scope,
        string? status,
        int? departmentId,
        int? positionId,
        int? jobPostingId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        CancellationToken cancellationToken)
    {
        var query = scope.ApplyToJobApplications(dbContext.JobApplications);

        if (!string.IsNullOrWhiteSpace(status) && ApplicationStatuses.IsDefined(status))
        {
            query = query.Where(application => application.Status == status);
        }

        if (departmentId.HasValue)
        {
            query = query.Where(
                application => application.JobPosting.Position.DepartmentId == departmentId.Value);
        }

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

    private async Task<IReadOnlyList<InterviewListItemViewModel>> BuildUpcomingInterviewsAsync(
        RecruitmentScope scope,
        int? departmentId,
        int? positionId,
        int? jobPostingId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        CancellationToken cancellationToken)
    {
        var query = scope.ApplyToInterviews(dbContext.Interviews)
            .Where(interview => interview.Status == InterviewStatuses.Scheduled);

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var lowerBoundUtc = dateFrom.HasValue
            ? Max(nowUtc, dateFrom.Value.ToDateTime(TimeOnly.MinValue))
            : nowUtc;
        query = query.Where(interview => interview.StartAtUtc >= lowerBoundUtc);

        if (dateTo.HasValue)
        {
            var toUtc = dateTo.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(interview => interview.StartAtUtc <= toUtc);
        }

        if (departmentId.HasValue)
        {
            query = query.Where(
                interview =>
                    interview.JobApplication.JobPosting.Position.DepartmentId == departmentId.Value);
        }

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

        return await query
            .OrderBy(interview => interview.StartAtUtc)
            .Take(MaxItemsPerSection)
            .Select(
                interview => new InterviewListItemViewModel(
                    interview.Id,
                    interview.JobApplication.CandidateProfile.FirstName + " " +
                        interview.JobApplication.CandidateProfile.LastName,
                    interview.JobApplication.JobPosting.Title,
                    InterviewTypes.GetDisplayLabel(interview.InterviewType),
                    interview.StartAtUtc,
                    interview.EndAtUtc,
                    InterviewStatuses.GetDisplayLabel(interview.Status)))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<OfferListItemViewModel>> BuildPendingOffersAsync(
        RecruitmentScope scope,
        int? departmentId,
        int? positionId,
        int? jobPostingId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        CancellationToken cancellationToken)
    {
        var query = scope.ApplyToOffers(dbContext.Offers)
            .Where(offer => offer.Status == OfferStatuses.PendingManagerApproval);

        if (departmentId.HasValue)
        {
            query = query.Where(
                offer =>
                    offer.JobApplication.JobPosting.Position.DepartmentId == departmentId.Value);
        }

        if (positionId.HasValue)
        {
            query = query.Where(
                offer => offer.JobApplication.JobPosting.PositionId == positionId.Value);
        }

        if (jobPostingId.HasValue)
        {
            query = query.Where(offer => offer.JobApplication.JobPostingId == jobPostingId.Value);
        }

        if (dateFrom.HasValue)
        {
            var fromUtc = dateFrom.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(offer => offer.CreatedAtUtc >= fromUtc);
        }

        if (dateTo.HasValue)
        {
            var toUtc = dateTo.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(offer => offer.CreatedAtUtc <= toUtc);
        }

        return await query
            .OrderBy(offer => offer.CreatedAtUtc)
            .Take(MaxItemsPerSection)
            .Select(
                offer => new OfferListItemViewModel(
                    offer.Id,
                    offer.JobApplication.CandidateProfile.FirstName + " " +
                        offer.JobApplication.CandidateProfile.LastName,
                    offer.JobApplication.JobPosting.Title,
                    offer.Salary,
                    offer.StartDate,
                    offer.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<JobPosting> ApplyJobPostingFilters(
        IQueryable<JobPosting> query,
        int? departmentId,
        int? positionId,
        int? jobPostingId)
    {
        if (departmentId.HasValue)
        {
            query = query.Where(
                jobPosting => jobPosting.Position.DepartmentId == departmentId.Value);
        }

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

    private async Task<DashboardFilterOptionsViewModel> BuildFilterOptionsAsync(
        RecruitmentScope scope,
        CancellationToken cancellationToken)
    {
        var scopedJobPostings = scope.ApplyToJobPostings(dbContext.JobPostings);

        var departmentOptions = await scopedJobPostings
            .Select(
                jobPosting => new
                {
                    jobPosting.Position.DepartmentId,
                    jobPosting.Position.Department.Name
                })
            .Distinct()
            .OrderBy(department => department.Name)
            .Select(department => new SelectOptionViewModel(department.DepartmentId, department.Name))
            .ToListAsync(cancellationToken);

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

        return new DashboardFilterOptionsViewModel(
            ApplicationStatuses.All.ToList(), departmentOptions, positionOptions, jobPostingOptions);
    }

    private static DateTime Max(DateTime first, DateTime second)
    {
        return first >= second ? first : second;
    }
}
