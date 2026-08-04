using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.Offers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize(Roles = $"{SystemRoles.Admin},{SystemRoles.RecruitmentSpecialist}")]
public sealed class OffersController(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IActivityLogService activityLogService,
    TimeProvider timeProvider) : Controller
{
    private const string InvalidApplicationStatusMessage =
        "Yalnızca Mülakat durumundaki bir başvuru için teklif oluşturulabilir.";

    private const string OfferAlreadyExistsMessage = "Bu başvuru için zaten bir teklif var.";

    private const string OfferCreatedMessage = "Teklif taslağı oluşturuldu.";

    private const string OfferUpdatedMessage = "Teklif güncellendi.";

    private const string MissingConcurrencyTokenMessage =
        "Sayfa bilgisi eksik, lütfen formu yeniden yükleyip tekrar deneyin.";

    private const string ConcurrencyConflictMessage =
        "Bu teklif siz işlem yaparken başka biri tarafından güncellendi, lütfen tekrar deneyin.";

    [HttpGet]
    public async Task<IActionResult> Create(int applicationId, CancellationToken cancellationToken)
    {
        var application = await LoadApplicationAsync(applicationId, cancellationToken);
        if (application is null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedForApplicationAsync(application.JobPosting))
        {
            return NotFound();
        }

        var validationError = await ValidateCreatePreconditionsAsync(application, cancellationToken);
        if (validationError is not null)
        {
            TempData["StatusMessage"] = validationError;
            return RedirectToAction("Details", "ApplicationsPool", new { id = applicationId });
        }

        return View(
            "Form",
            new OfferFormViewModel
            {
                JobApplicationId = application.Id,
                CandidateName =
                    $"{application.CandidateProfile.FirstName} {application.CandidateProfile.LastName}",
                JobPostingTitle = application.JobPosting.Title
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        int applicationId,
        OfferFormViewModel model,
        CancellationToken cancellationToken)
    {
        var application = await LoadApplicationAsync(applicationId, cancellationToken);
        if (application is null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedForApplicationAsync(application.JobPosting))
        {
            return NotFound();
        }

        var validationError = await ValidateCreatePreconditionsAsync(application, cancellationToken);
        if (validationError is not null)
        {
            TempData["StatusMessage"] = validationError;
            return RedirectToAction("Details", "ApplicationsPool", new { id = applicationId });
        }

        model.JobApplicationId = application.Id;
        model.CandidateName =
            $"{application.CandidateProfile.FirstName} {application.CandidateProfile.LastName}";
        model.JobPostingTitle = application.JobPosting.Title;

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        var actorUserId = userManager.GetUserId(User);
        if (actorUserId is null)
        {
            return Forbid();
        }

        Offer offer;
        try
        {
            offer = new Offer(
                application.Id,
                actorUserId,
                model.Salary!.Value,
                model.StartDate!.Value,
                model.Note,
                timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("Form", model);
        }

        dbContext.Offers.Add(offer);
        await dbContext.SaveChangesAsync(cancellationToken);

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityCreated,
                "Uzman teklif taslağı oluşturdu.",
                ActivityEntityTypes.Offer,
                offer.Id.ToString(),
                JobPostingId: application.JobPostingId.ToString(),
                CandidateId: application.CandidateProfile.ApplicationUserId));
        await dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = OfferCreatedMessage;
        return RedirectToAction(nameof(Edit), new { id = offer.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var offer = await LoadOfferAsync(id, cancellationToken);
        if (offer is null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedForApplicationAsync(offer.JobApplication.JobPosting))
        {
            return NotFound();
        }

        return View(
            "Form",
            new OfferFormViewModel
            {
                OfferId = offer.Id,
                JobApplicationId = offer.JobApplicationId,
                CandidateName =
                    $"{offer.JobApplication.CandidateProfile.FirstName} " +
                    $"{offer.JobApplication.CandidateProfile.LastName}",
                JobPostingTitle = offer.JobApplication.JobPosting.Title,
                Salary = offer.Salary,
                StartDate = offer.StartDate,
                Note = offer.Note,
                RowVersion = Convert.ToBase64String(offer.RowVersion)
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        OfferFormViewModel model,
        CancellationToken cancellationToken)
    {
        var offer = await LoadOfferAsync(id, cancellationToken);
        if (offer is null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedForApplicationAsync(offer.JobApplication.JobPosting))
        {
            return NotFound();
        }

        model.OfferId = offer.Id;
        model.JobApplicationId = offer.JobApplicationId;
        model.CandidateName =
            $"{offer.JobApplication.CandidateProfile.FirstName} " +
            $"{offer.JobApplication.CandidateProfile.LastName}";
        model.JobPostingTitle = offer.JobApplication.JobPosting.Title;

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        if (string.IsNullOrEmpty(model.RowVersion))
        {
            ModelState.AddModelError(string.Empty, MissingConcurrencyTokenMessage);
            return View("Form", model);
        }

        dbContext.Entry(offer).Property(entity => entity.RowVersion).OriginalValue =
            Convert.FromBase64String(model.RowVersion);

        try
        {
            offer.EditDraft(model.Salary!.Value, model.StartDate!.Value, model.Note);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("Form", model);
        }
        catch (InvalidOperationException exception)
        {
            TempData["StatusMessage"] = exception.Message;
            return RedirectToAction(nameof(Edit), new { id });
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityUpdated,
                "Uzman teklif taslağını güncelledi.",
                ActivityEntityTypes.Offer,
                offer.Id.ToString(),
                JobPostingId: offer.JobApplication.JobPostingId.ToString(),
                CandidateId: offer.JobApplication.CandidateProfile.ApplicationUserId));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["StatusMessage"] = ConcurrencyConflictMessage;
            return RedirectToAction(nameof(Edit), new { id });
        }

        TempData["StatusMessage"] = OfferUpdatedMessage;
        return RedirectToAction(nameof(Edit), new { id });
    }

    private async Task<string?> ValidateCreatePreconditionsAsync(
        JobApplication application,
        CancellationToken cancellationToken)
    {
        if (application.Status != ApplicationStatuses.Interview)
        {
            return InvalidApplicationStatusMessage;
        }

        var offerAlreadyExists = await dbContext.Offers
            .AnyAsync(offer => offer.JobApplicationId == application.Id, cancellationToken);

        return offerAlreadyExists ? OfferAlreadyExistsMessage : null;
    }

    private Task<JobApplication?> LoadApplicationAsync(int applicationId, CancellationToken cancellationToken)
    {
        return dbContext.JobApplications
            .Include(application => application.JobPosting)
            .ThenInclude(jobPosting => jobPosting.Position)
            .Include(application => application.CandidateProfile)
            .FirstOrDefaultAsync(application => application.Id == applicationId, cancellationToken);
    }

    private Task<Offer?> LoadOfferAsync(int offerId, CancellationToken cancellationToken)
    {
        return dbContext.Offers
            .Include(offer => offer.JobApplication)
            .ThenInclude(application => application.JobPosting)
            .ThenInclude(jobPosting => jobPosting.Position)
            .Include(offer => offer.JobApplication)
            .ThenInclude(application => application.CandidateProfile)
            .FirstOrDefaultAsync(offer => offer.Id == offerId, cancellationToken);
    }

    private async Task<bool> IsAuthorizedForApplicationAsync(JobPosting jobPosting)
    {
        if (User.IsInRole(SystemRoles.Admin))
        {
            return true;
        }

        var currentUser = await userManager.GetUserAsync(User);
        return currentUser is not null && jobPosting.ResponsibleUserId == currentUser.Id;
    }
}
