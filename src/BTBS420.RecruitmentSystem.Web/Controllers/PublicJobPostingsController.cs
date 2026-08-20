using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.Positions;
using BTBS420.RecruitmentSystem.Web.ViewModels.PublicJobPostings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[AllowAnonymous]
public sealed class PublicJobPostingsController(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider) : Controller
{
    private const int DefaultPageSize = 10;
    private const int MaximumPageSize = 50;

    [HttpGet]
    public async Task<IActionResult> Index(
        int? positionId,
        int page = 1,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);

        var query = dbContext.JobPostings.Where(
            jobPosting => jobPosting.Status == JobPostingStatuses.Published && !jobPosting.IsInternal);

        if (positionId.HasValue)
        {
            query = query.Where(jobPosting => jobPosting.PositionId == positionId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var jobPostings = await query
            .OrderBy(jobPosting => jobPosting.ApplicationDeadline)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(
                jobPosting => new PublicJobPostingListItemViewModel(
                    jobPosting.Id,
                    jobPosting.Title,
                    jobPosting.Position.Name,
                    jobPosting.Position.Department.Name,
                    jobPosting.ApplicationDeadline))
            .ToListAsync(cancellationToken);

        var positionOptions = await dbContext.Positions
            .Where(position => position.IsActive)
            .OrderBy(position => position.Name)
            .Select(position => new SelectOptionViewModel(position.Id, position.Name))
            .ToListAsync(cancellationToken);

        return View(
            new PublicJobPostingIndexViewModel(
                jobPostings,
                positionOptions,
                positionId,
                page,
                pageSize,
                totalCount));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var jobPosting = await dbContext.JobPostings
            .Include(entity => entity.Position)
            .ThenInclude(position => position.Department)
            .Where(
                entity => entity.Status == JobPostingStatuses.Published && !entity.IsInternal)
            .FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (jobPosting is null)
        {
            return NotFound();
        }

        var canApply = false;
        var alreadyApplied = false;

        if (User.IsInRole(SystemRoles.Candidate))
        {
            var userId = userManager.GetUserId(User);
            var profile = userId is null
                ? null
                : await dbContext.CandidateProfiles
                    .FirstOrDefaultAsync(profile => profile.ApplicationUserId == userId, cancellationToken);

            if (profile is not null)
            {
                var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
                canApply = jobPosting.ApplicationDeadline >= today;

                alreadyApplied = await dbContext.JobApplications.AnyAsync(
                    application =>
                        application.JobPostingId == jobPosting.Id &&
                        application.CandidateProfileId == profile.Id,
                    cancellationToken);
            }
        }

        return View(
            new PublicJobPostingDetailViewModel(
                jobPosting.Id,
                jobPosting.Title,
                jobPosting.Description,
                jobPosting.Position.Name,
                jobPosting.Position.Department.Name,
                jobPosting.ApplicationDeadline,
                canApply && !alreadyApplied,
                alreadyApplied));
    }
}
