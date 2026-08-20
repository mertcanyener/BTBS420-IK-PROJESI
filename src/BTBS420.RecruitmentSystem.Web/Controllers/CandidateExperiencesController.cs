using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.CandidateExperiences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize(Roles = SystemRoles.Candidate)]
public sealed class CandidateExperiencesController(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IActivityLogService activityLogService) : Controller
{
    private const string ProfileRequiredMessage =
        "İş deneyimi eklemeden önce profilinizi oluşturmalısınız.";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var profile = await GetCurrentProfileAsync(cancellationToken);
        if (profile is null)
        {
            TempData["StatusMessage"] = ProfileRequiredMessage;
            return RedirectToAction("Index", "CandidateProfile");
        }

        var experiences = await dbContext.CandidateExperiences
            .Where(experience => experience.CandidateProfileId == profile.Id)
            .OrderByDescending(experience => experience.StartDate)
            .Select(
                experience => new CandidateExperienceListItemViewModel(
                    experience.Id,
                    experience.CompanyName,
                    experience.JobTitle,
                    experience.StartDate,
                    experience.EndDate))
            .ToListAsync(cancellationToken);

        return View(new CandidateExperienceIndexViewModel(experiences));
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

        return View(new CandidateExperienceFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CandidateExperienceFormViewModel model,
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
            return View(model);
        }

        var endDate = model.IsOngoing ? null : model.EndDate;

        CandidateExperience experience;
        try
        {
            experience = new CandidateExperience(
                profile.Id,
                model.CompanyName,
                model.JobTitle,
                model.StartDate!.Value,
                endDate);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }

        dbContext.CandidateExperiences.Add(experience);
        await dbContext.SaveChangesAsync(cancellationToken);

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityCreated,
                "Aday iş deneyimi kaydı oluşturuldu.",
                ActivityEntityTypes.CandidateExperience,
                experience.Id.ToString(),
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

        var experience = await dbContext.CandidateExperiences
            .FirstOrDefaultAsync(
                candidateExperience =>
                    candidateExperience.Id == id &&
                    candidateExperience.CandidateProfileId == profile.Id,
                cancellationToken);

        if (experience is null)
        {
            return NotFound();
        }

        var model = new CandidateExperienceFormViewModel
        {
            Id = experience.Id,
            CompanyName = experience.CompanyName,
            JobTitle = experience.JobTitle,
            StartDate = experience.StartDate,
            EndDate = experience.EndDate,
            IsOngoing = experience.EndDate is null
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        CandidateExperienceFormViewModel model,
        CancellationToken cancellationToken)
    {
        var profile = await GetCurrentProfileAsync(cancellationToken);
        if (profile is null)
        {
            TempData["StatusMessage"] = ProfileRequiredMessage;
            return RedirectToAction("Index", "CandidateProfile");
        }

        var experience = await dbContext.CandidateExperiences
            .FirstOrDefaultAsync(
                candidateExperience =>
                    candidateExperience.Id == id &&
                    candidateExperience.CandidateProfileId == profile.Id,
                cancellationToken);

        if (experience is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var endDate = model.IsOngoing ? null : model.EndDate;

        try
        {
            experience.Edit(
                model.CompanyName,
                model.JobTitle,
                model.StartDate!.Value,
                endDate);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityUpdated,
                "Aday iş deneyimi kaydı güncellendi.",
                ActivityEntityTypes.CandidateExperience,
                experience.Id.ToString(),
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

        var experience = await dbContext.CandidateExperiences
            .FirstOrDefaultAsync(
                candidateExperience =>
                    candidateExperience.Id == id &&
                    candidateExperience.CandidateProfileId == profile.Id,
                cancellationToken);

        if (experience is null)
        {
            return NotFound();
        }

        dbContext.CandidateExperiences.Remove(experience);

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityDeleted,
                "Aday iş deneyimi kaydı silindi.",
                ActivityEntityTypes.CandidateExperience,
                experience.Id.ToString(),
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
}
