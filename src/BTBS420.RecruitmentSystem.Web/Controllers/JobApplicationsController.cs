using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.JobApplications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize(Roles = SystemRoles.Candidate)]
public sealed class JobApplicationsController(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IActivityLogService activityLogService,
    TimeProvider timeProvider) : Controller
{
    private const string ProfileRequiredMessage =
        "Başvurmadan önce profilinizi oluşturmalısınız.";

    private const string JobPostingNotEligibleMessage =
        "Bu ilan başvuruya açık değil.";

    private const string DuplicateApplicationMessage =
        "Bu ilana zaten başvurdunuz.";

    private const string ApplicationSucceededMessage = "Başvurunuz alındı.";

    private const string WithdrawSucceededMessage = "Başvurunuz geri çekildi.";

    private const string ConcurrencyConflictMessage =
        "Başvurunuz siz işlem yaparken başka bir işlemle güncellendi, lütfen tekrar deneyin.";

    private const string OfferNotAvailableMessage = "Karar verebileceğiniz bir teklif bulunamadı.";

    private const string OfferAcceptedMessage = "Teklifi kabul ettiniz, başvurunuz İşe Alındı olarak güncellendi.";

    private const string OfferRejectedMessage = "Teklifi reddettiniz.";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var profile = await GetCurrentProfileAsync(cancellationToken);
        if (profile is null)
        {
            TempData["StatusMessage"] = ProfileRequiredMessage;
            return RedirectToAction("Index", "CandidateProfile");
        }

        var applications = await dbContext.JobApplications
            .Where(application => application.CandidateProfileId == profile.Id)
            .OrderByDescending(application => application.AppliedAtUtc)
            .Select(
                application => new
                {
                    application.Id,
                    application.JobPostingId,
                    JobPostingTitle = application.JobPosting.Title,
                    PositionName = application.JobPosting.Position.Name,
                    JobPostingStatus = application.JobPosting.Status,
                    application.Status,
                    application.AppliedAtUtc,
                    application.WithdrawnAtUtc,
                    Offer = dbContext.Offers
                        .Where(
                            offer =>
                                offer.JobApplicationId == application.Id &&
                                (offer.Status == OfferStatuses.Approved ||
                                    offer.Status == OfferStatuses.Accepted ||
                                    offer.Status == OfferStatuses.RejectedByCandidate))
                        .Select(
                            offer => new
                            {
                                offer.Id,
                                offer.Status,
                                offer.Salary,
                                offer.StartDate,
                                offer.Note
                            })
                        .FirstOrDefault()
                })
            .ToListAsync(cancellationToken);

        var listItems = applications
            .Select(
                application => new JobApplicationListItemViewModel(
                    application.Id,
                    application.JobPostingId,
                    application.JobPostingTitle,
                    application.PositionName,
                    application.JobPostingStatus,
                    ApplicationStatuses.GetDisplayLabel(application.Status),
                    application.AppliedAtUtc,
                    application.WithdrawnAtUtc,
                    ApplicationStatuses.CanWithdraw(application.Status),
                    application.Offer?.Id,
                    application.Offer is null
                        ? null
                        : OfferStatuses.GetDisplayLabel(application.Offer.Status),
                    application.Offer?.Salary,
                    application.Offer?.StartDate,
                    application.Offer?.Note,
                    application.Offer is not null && application.Offer.Status == OfferStatuses.Approved))
            .ToList();

        return View(new JobApplicationIndexViewModel(listItems));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(int id, CancellationToken cancellationToken)
    {
        var profile = await GetCurrentProfileAsync(cancellationToken);
        if (profile is null)
        {
            TempData["StatusMessage"] = ProfileRequiredMessage;
            return RedirectToAction("Index", "CandidateProfile");
        }

        var application = await dbContext.JobApplications
            .FirstOrDefaultAsync(
                candidateApplication =>
                    candidateApplication.Id == id &&
                    candidateApplication.CandidateProfileId == profile.Id,
                cancellationToken);

        if (application is null)
        {
            return NotFound();
        }

        JobApplicationStatusChange statusChange;
        try
        {
            statusChange = application.Withdraw(
                profile.ApplicationUserId,
                timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (InvalidOperationException exception)
        {
            TempData["StatusMessage"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }

        dbContext.JobApplicationStatusChanges.Add(statusChange);

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "Aday başvurusunu geri çekti.",
                ActivityEntityTypes.Application,
                application.Id.ToString(),
                JobPostingId: application.JobPostingId.ToString(),
                CandidateId: profile.ApplicationUserId));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["StatusMessage"] = ConcurrencyConflictMessage;
            return RedirectToAction(nameof(Index));
        }

        TempData["StatusMessage"] = WithdrawSucceededMessage;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptOffer(int id, CancellationToken cancellationToken)
    {
        var profile = await GetCurrentProfileAsync(cancellationToken);
        if (profile is null)
        {
            TempData["StatusMessage"] = ProfileRequiredMessage;
            return RedirectToAction("Index", "CandidateProfile");
        }

        var application = await dbContext.JobApplications
            .FirstOrDefaultAsync(
                candidateApplication =>
                    candidateApplication.Id == id && candidateApplication.CandidateProfileId == profile.Id,
                cancellationToken);
        if (application is null)
        {
            return NotFound();
        }

        var offer = await dbContext.Offers
            .FirstOrDefaultAsync(o => o.JobApplicationId == application.Id, cancellationToken);
        if (offer is null || offer.Status != OfferStatuses.Approved)
        {
            TempData["StatusMessage"] = OfferNotAvailableMessage;
            return RedirectToAction(nameof(Index));
        }

        var changedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        OfferStatusChange offerStatusChange;
        JobApplicationStatusChange applicationStatusChange;
        try
        {
            offerStatusChange = offer.Accept(profile.ApplicationUserId, changedAtUtc);
            applicationStatusChange = application.TransitionTo(
                ApplicationStatuses.Hired,
                profile.ApplicationUserId,
                reason: null,
                changedAtUtc);
        }
        catch (InvalidOperationException exception)
        {
            TempData["StatusMessage"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }

        dbContext.OfferStatusChanges.Add(offerStatusChange);
        dbContext.JobApplicationStatusChanges.Add(applicationStatusChange);

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "Aday teklifi kabul etti.",
                ActivityEntityTypes.Offer,
                offer.Id.ToString(),
                JobPostingId: application.JobPostingId.ToString(),
                CandidateId: profile.ApplicationUserId));
        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "Aday teklifi kabul etti, başvuru İşe Alındı oldu.",
                ActivityEntityTypes.Application,
                application.Id.ToString(),
                JobPostingId: application.JobPostingId.ToString(),
                CandidateId: profile.ApplicationUserId));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["StatusMessage"] = ConcurrencyConflictMessage;
            return RedirectToAction(nameof(Index));
        }

        TempData["StatusMessage"] = OfferAcceptedMessage;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectOffer(int id, CancellationToken cancellationToken)
    {
        var profile = await GetCurrentProfileAsync(cancellationToken);
        if (profile is null)
        {
            TempData["StatusMessage"] = ProfileRequiredMessage;
            return RedirectToAction("Index", "CandidateProfile");
        }

        var application = await dbContext.JobApplications
            .FirstOrDefaultAsync(
                candidateApplication =>
                    candidateApplication.Id == id && candidateApplication.CandidateProfileId == profile.Id,
                cancellationToken);
        if (application is null)
        {
            return NotFound();
        }

        var offer = await dbContext.Offers
            .FirstOrDefaultAsync(o => o.JobApplicationId == application.Id, cancellationToken);
        if (offer is null || offer.Status != OfferStatuses.Approved)
        {
            TempData["StatusMessage"] = OfferNotAvailableMessage;
            return RedirectToAction(nameof(Index));
        }

        var changedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        OfferStatusChange offerStatusChange;
        JobApplicationStatusChange applicationStatusChange;
        try
        {
            offerStatusChange = offer.RejectByCandidate(profile.ApplicationUserId, changedAtUtc);
            applicationStatusChange = application.TransitionTo(
                ApplicationStatuses.Rejected,
                profile.ApplicationUserId,
                reason: null,
                changedAtUtc);
        }
        catch (InvalidOperationException exception)
        {
            TempData["StatusMessage"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }

        dbContext.OfferStatusChanges.Add(offerStatusChange);
        dbContext.JobApplicationStatusChanges.Add(applicationStatusChange);

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "Aday teklifi reddetti.",
                ActivityEntityTypes.Offer,
                offer.Id.ToString(),
                JobPostingId: application.JobPostingId.ToString(),
                CandidateId: profile.ApplicationUserId));
        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "Aday teklifi reddetti, başvuru reddedildi.",
                ActivityEntityTypes.Application,
                application.Id.ToString(),
                JobPostingId: application.JobPostingId.ToString(),
                CandidateId: profile.ApplicationUserId));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["StatusMessage"] = ConcurrencyConflictMessage;
            return RedirectToAction(nameof(Index));
        }

        TempData["StatusMessage"] = OfferRejectedMessage;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int jobPostingId, CancellationToken cancellationToken)
    {
        var profile = await GetCurrentProfileAsync(cancellationToken);
        if (profile is null)
        {
            TempData["StatusMessage"] = ProfileRequiredMessage;
            return RedirectToAction("Details", "PublicJobPostings", new { id = jobPostingId });
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var isEligible = await dbContext.JobPostings.AnyAsync(
            jobPosting =>
                jobPosting.Id == jobPostingId &&
                jobPosting.Status == JobPostingStatuses.Published &&
                !jobPosting.IsInternal &&
                jobPosting.ApplicationDeadline >= today,
            cancellationToken);

        if (!isEligible)
        {
            TempData["StatusMessage"] = JobPostingNotEligibleMessage;
            return RedirectToAction("Details", "PublicJobPostings", new { id = jobPostingId });
        }

        var alreadyApplied = await dbContext.JobApplications.AnyAsync(
            application =>
                application.JobPostingId == jobPostingId &&
                application.CandidateProfileId == profile.Id,
            cancellationToken);

        if (alreadyApplied)
        {
            TempData["StatusMessage"] = DuplicateApplicationMessage;
            return RedirectToAction("Details", "PublicJobPostings", new { id = jobPostingId });
        }

        var application = new JobApplication(
            jobPostingId,
            profile.Id,
            timeProvider.GetUtcNow().UtcDateTime);
        dbContext.JobApplications.Add(application);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            TempData["StatusMessage"] = DuplicateApplicationMessage;
            return RedirectToAction("Details", "PublicJobPostings", new { id = jobPostingId });
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityCreated,
                "Aday ilana başvurdu.",
                ActivityEntityTypes.Application,
                application.Id.ToString(),
                JobPostingId: jobPostingId.ToString(),
                CandidateId: profile.ApplicationUserId));
        await dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = ApplicationSucceededMessage;
        return RedirectToAction("Details", "PublicJobPostings", new { id = jobPostingId });
    }

    private async Task<CandidateProfile?> GetCurrentProfileAsync(CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null)
        {
            return null;
        }

        return await dbContext.CandidateProfiles
            .FirstOrDefaultAsync(profile => profile.ApplicationUserId == userId, cancellationToken);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException &&
            sqlException.Number is 2601 or 2627;
    }
}
