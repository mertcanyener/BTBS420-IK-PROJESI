using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.CandidateEducations;
using BTBS420.RecruitmentSystem.Web.ViewModels.Positions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize(Roles = SystemRoles.Candidate)]
public sealed class CandidateEducationsController(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IActivityLogService activityLogService) : Controller
{
    private const string InactiveEducationMessage =
        "Seçilen eğitim seviyesi aktif değil veya bulunamadı.";

    private const string ProfileRequiredMessage =
        "Eğitim bilgisi eklemeden önce profilinizi oluşturmalısınız.";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var profile = await GetCurrentProfileAsync(cancellationToken);
        if (profile is null)
        {
            TempData["StatusMessage"] = ProfileRequiredMessage;
            return RedirectToAction("Index", "CandidateProfile");
        }

        var educations = await dbContext.CandidateEducations
            .Where(education => education.CandidateProfileId == profile.Id)
            .OrderByDescending(education => education.StartDate)
            .Select(
                education => new CandidateEducationListItemViewModel(
                    education.Id,
                    education.Education.Name,
                    education.SchoolName,
                    education.FieldOfStudy,
                    education.StartDate,
                    education.EndDate))
            .ToListAsync(cancellationToken);

        return View(new CandidateEducationIndexViewModel(educations));
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var profile = await GetCurrentProfileAsync(cancellationToken);
        if (profile is null)
        {
            TempData["StatusMessage"] = ProfileRequiredMessage;
            return RedirectToAction("Index", "CandidateProfile");
        }

        return View(await BuildFormWithOptionsAsync(new CandidateEducationFormViewModel(), cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CandidateEducationFormViewModel model,
        CancellationToken cancellationToken)
    {
        var profile = await GetCurrentProfileAsync(cancellationToken);
        if (profile is null)
        {
            TempData["StatusMessage"] = ProfileRequiredMessage;
            return RedirectToAction("Index", "CandidateProfile");
        }

        if (!ModelState.IsValid)
        {
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        if (!await ValidateEducationAsync(model.EducationId!.Value, cancellationToken))
        {
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        var endDate = model.IsOngoing ? null : model.EndDate;

        CandidateEducation education;
        try
        {
            education = new CandidateEducation(
                profile.Id,
                model.EducationId!.Value,
                model.SchoolName,
                model.FieldOfStudy,
                model.StartDate!.Value,
                endDate);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        dbContext.CandidateEducations.Add(education);
        await dbContext.SaveChangesAsync(cancellationToken);

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityCreated,
                "Aday eğitim kaydı oluşturuldu.",
                ActivityEntityTypes.CandidateEducation,
                education.Id.ToString(),
                CandidateId: profile.ApplicationUserId));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var profile = await GetCurrentProfileAsync(cancellationToken);
        if (profile is null)
        {
            TempData["StatusMessage"] = ProfileRequiredMessage;
            return RedirectToAction("Index", "CandidateProfile");
        }

        var education = await dbContext.CandidateEducations
            .FirstOrDefaultAsync(
                candidateEducation =>
                    candidateEducation.Id == id &&
                    candidateEducation.CandidateProfileId == profile.Id,
                cancellationToken);

        if (education is null)
        {
            return NotFound();
        }

        var model = new CandidateEducationFormViewModel
        {
            Id = education.Id,
            EducationId = education.EducationId,
            SchoolName = education.SchoolName,
            FieldOfStudy = education.FieldOfStudy,
            StartDate = education.StartDate,
            EndDate = education.EndDate,
            IsOngoing = education.EndDate is null
        };

        return View(await BuildFormWithOptionsAsync(model, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        CandidateEducationFormViewModel model,
        CancellationToken cancellationToken)
    {
        var profile = await GetCurrentProfileAsync(cancellationToken);
        if (profile is null)
        {
            TempData["StatusMessage"] = ProfileRequiredMessage;
            return RedirectToAction("Index", "CandidateProfile");
        }

        var education = await dbContext.CandidateEducations
            .FirstOrDefaultAsync(
                candidateEducation =>
                    candidateEducation.Id == id &&
                    candidateEducation.CandidateProfileId == profile.Id,
                cancellationToken);

        if (education is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        if (!await ValidateEducationAsync(model.EducationId!.Value, cancellationToken))
        {
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        var endDate = model.IsOngoing ? null : model.EndDate;

        try
        {
            education.Edit(
                model.EducationId!.Value,
                model.SchoolName,
                model.FieldOfStudy,
                model.StartDate!.Value,
                endDate);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityUpdated,
                "Aday eğitim kaydı güncellendi.",
                ActivityEntityTypes.CandidateEducation,
                education.Id.ToString(),
                CandidateId: profile.ApplicationUserId));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var profile = await GetCurrentProfileAsync(cancellationToken);
        if (profile is null)
        {
            TempData["StatusMessage"] = ProfileRequiredMessage;
            return RedirectToAction("Index", "CandidateProfile");
        }

        var education = await dbContext.CandidateEducations
            .FirstOrDefaultAsync(
                candidateEducation =>
                    candidateEducation.Id == id &&
                    candidateEducation.CandidateProfileId == profile.Id,
                cancellationToken);

        if (education is null)
        {
            return NotFound();
        }

        dbContext.CandidateEducations.Remove(education);

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityDeleted,
                "Aday eğitim kaydı silindi.",
                ActivityEntityTypes.CandidateEducation,
                education.Id.ToString(),
                CandidateId: profile.ApplicationUserId));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
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

    private async Task<bool> ValidateEducationAsync(int educationId, CancellationToken cancellationToken)
    {
        var education = await dbContext.Educations.FindAsync([educationId], cancellationToken);
        if (education is null || !education.IsActive)
        {
            ModelState.AddModelError(
                nameof(CandidateEducationFormViewModel.EducationId),
                InactiveEducationMessage);
            return false;
        }

        return true;
    }

    private async Task<CandidateEducationFormViewModel> BuildFormWithOptionsAsync(
        CandidateEducationFormViewModel model,
        CancellationToken cancellationToken)
    {
        var options = await dbContext.Educations
            .Where(education => education.IsActive)
            .OrderBy(education => education.Name)
            .Select(education => new SelectOptionViewModel(education.Id, education.Name))
            .ToListAsync(cancellationToken);

        if (model.EducationId.HasValue &&
            options.All(option => option.Id != model.EducationId.Value))
        {
            var currentEducation = await dbContext.Educations.FindAsync(
                [model.EducationId.Value],
                cancellationToken);
            if (currentEducation is not null)
            {
                options = [.. options, new SelectOptionViewModel(currentEducation.Id, $"{currentEducation.Name} (pasif)")];
            }
        }

        model.EducationOptions = options;

        return model;
    }
}
