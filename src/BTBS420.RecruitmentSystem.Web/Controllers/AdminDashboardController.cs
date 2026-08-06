using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.Dashboard;
using BTBS420.RecruitmentSystem.Web.ViewModels.Positions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminDashboardController(
    ApplicationDbContext dbContext,
    IRecruitmentScopeService scopeService) : Controller
{
    private static readonly string[] InProgressApplicationStatuses =
    [
        ApplicationStatuses.New,
        ApplicationStatuses.Screening,
        ApplicationStatuses.Interview
    ];

    [HttpGet]
    public async Task<IActionResult> Index(
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

        var jobPostingQuery = ApplyJobPostingFilters(
            scope.ApplyToJobPostings(dbContext.JobPostings), departmentId, positionId, jobPostingId);

        var jobApplicationQuery = ApplyJobApplicationFilters(
            scope.ApplyToJobApplications(dbContext.JobApplications),
            departmentId,
            positionId,
            jobPostingId,
            dateFrom,
            dateTo);

        var totalUsersQuery = dbContext.Users.Where(user => user.IsActive);
        if (departmentId.HasValue)
        {
            totalUsersQuery = totalUsersQuery.Where(user => user.DepartmentId == departmentId.Value);
        }

        var metrics = new AdminDashboardMetricsViewModel(
            await totalUsersQuery.CountAsync(cancellationToken),
            await jobPostingQuery
                .Where(jobPosting => jobPosting.Status == JobPostingStatuses.Published)
                .CountAsync(cancellationToken),
            await jobApplicationQuery.CountAsync(cancellationToken),
            await jobApplicationQuery
                .Where(application => InProgressApplicationStatuses.Contains(application.Status))
                .CountAsync(cancellationToken),
            await jobApplicationQuery
                .Where(application => application.Status == ApplicationStatuses.Hired)
                .CountAsync(cancellationToken));

        var filter = new AdminDashboardFilterViewModel(
            departmentId, positionId, jobPostingId, dateFrom, dateTo);
        var filterOptions = await BuildFilterOptionsAsync(scope, cancellationToken);

        return View(new AdminDashboardViewModel(metrics, filter, filterOptions));
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

    private static IQueryable<JobApplication> ApplyJobApplicationFilters(
        IQueryable<JobApplication> query,
        int? departmentId,
        int? positionId,
        int? jobPostingId,
        DateOnly? dateFrom,
        DateOnly? dateTo)
    {
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

        return query;
    }

    private async Task<AdminDashboardFilterOptionsViewModel> BuildFilterOptionsAsync(
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

        return new AdminDashboardFilterOptionsViewModel(
            departmentOptions, positionOptions, jobPostingOptions);
    }
}
