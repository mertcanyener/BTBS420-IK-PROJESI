using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.CandidateProfiles;
using BTBS420.RecruitmentSystem.Web.ViewModels.Positions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize(Roles = SystemRoles.Candidate)]
public sealed class CandidateProfileController(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IActivityLogService activityLogService) : Controller
{
    private const string InactiveTargetPositionMessage =
        "Seçilen hedef pozisyon aktif değil veya bulunamadı.";

    private const string InvalidSkillSelectionMessage =
        "Seçilen yetkinliklerden biri veya birkaçı aktif değil ya da bulunamadı.";

    private const string InvalidLanguageSelectionMessage =
        "Seçilen dillerden biri veya birkaçı aktif değil ya da bulunamadı.";

    private const string DuplicateProfileMessage =
        "Bu kullanıcı için zaten bir profil kaydı mevcut.";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null)
        {
            return Forbid();
        }

        var profile = await dbContext.CandidateProfiles
            .FirstOrDefaultAsync(candidate => candidate.ApplicationUserId == userId, cancellationToken);

        var model = await LoadFormAsync(profile, cancellationToken);

        return View(await BuildFormWithOptionsAsync(model, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(
        CandidateProfileFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        var userId = userManager.GetUserId(User);
        if (userId is null)
        {
            return Forbid();
        }

        if (!await ValidateAssociationsAsync(model, cancellationToken))
        {
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        var profile = await dbContext.CandidateProfiles
            .FirstOrDefaultAsync(candidate => candidate.ApplicationUserId == userId, cancellationToken);
        var isNewProfile = profile is null;

        try
        {
            if (profile is null)
            {
                profile = new CandidateProfile(
                    userId,
                    model.FirstName,
                    model.LastName,
                    model.ProfessionalSummary,
                    model.TargetPositionId);
                dbContext.CandidateProfiles.Add(profile);
            }
            else
            {
                profile.Edit(
                    model.FirstName,
                    model.LastName,
                    model.ProfessionalSummary,
                    model.TargetPositionId);
            }
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            ModelState.AddModelError(string.Empty, DuplicateProfileMessage);
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        await SyncSkillsAsync(profile.Id, model.SelectedSkillIds, cancellationToken);
        await SyncLanguagesAsync(profile.Id, model.SelectedLanguageIds, cancellationToken);

        activityLogService.Stage(
            new ActivityLogEntry(
                isNewProfile ? ActivityActionCodes.EntityCreated : ActivityActionCodes.EntityUpdated,
                isNewProfile ? "Aday profili oluşturuldu." : "Aday profili güncellendi.",
                ActivityEntityTypes.Candidate,
                profile.Id.ToString(),
                CandidateId: userId));
        await dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = "Profiliniz kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<CandidateProfileFormViewModel> LoadFormAsync(
        CandidateProfile? profile,
        CancellationToken cancellationToken)
    {
        if (profile is null)
        {
            return new CandidateProfileFormViewModel();
        }

        var model = new CandidateProfileFormViewModel
        {
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            ProfessionalSummary = profile.ProfessionalSummary,
            TargetPositionId = profile.TargetPositionId
        };

        model.SelectedSkillIds = await dbContext.CandidateProfileSkills
            .Where(link => link.CandidateProfileId == profile.Id)
            .Select(link => link.SkillId)
            .ToListAsync(cancellationToken);

        model.SelectedLanguageIds = await dbContext.CandidateProfileLanguages
            .Where(link => link.CandidateProfileId == profile.Id)
            .Select(link => link.LanguageId)
            .ToListAsync(cancellationToken);

        return model;
    }

    private async Task<bool> ValidateAssociationsAsync(
        CandidateProfileFormViewModel model,
        CancellationToken cancellationToken)
    {
        var isValid = true;

        if (model.TargetPositionId.HasValue)
        {
            var position = await dbContext.Positions.FindAsync(
                [model.TargetPositionId.Value],
                cancellationToken);
            if (position is null || !position.IsActive)
            {
                ModelState.AddModelError(nameof(model.TargetPositionId), InactiveTargetPositionMessage);
                isValid = false;
            }
        }

        var selectedSkillIds = model.SelectedSkillIds.Distinct().ToList();
        if (selectedSkillIds.Count > 0)
        {
            var activeSkillCount = await dbContext.Skills
                .CountAsync(
                    skill => selectedSkillIds.Contains(skill.Id) && skill.IsActive,
                    cancellationToken);
            if (activeSkillCount != selectedSkillIds.Count)
            {
                ModelState.AddModelError(nameof(model.SelectedSkillIds), InvalidSkillSelectionMessage);
                isValid = false;
            }
        }

        var selectedLanguageIds = model.SelectedLanguageIds.Distinct().ToList();
        if (selectedLanguageIds.Count > 0)
        {
            var activeLanguageCount = await dbContext.Languages
                .CountAsync(
                    language => selectedLanguageIds.Contains(language.Id) && language.IsActive,
                    cancellationToken);
            if (activeLanguageCount != selectedLanguageIds.Count)
            {
                ModelState.AddModelError(nameof(model.SelectedLanguageIds), InvalidLanguageSelectionMessage);
                isValid = false;
            }
        }

        return isValid;
    }

    private async Task SyncSkillsAsync(
        int candidateProfileId,
        IReadOnlyCollection<int> selectedSkillIds,
        CancellationToken cancellationToken)
    {
        var distinctIds = selectedSkillIds.Distinct().ToList();

        var existingLinks = await dbContext.CandidateProfileSkills
            .Where(link => link.CandidateProfileId == candidateProfileId)
            .ToListAsync(cancellationToken);

        var toRemove = existingLinks.Where(link => !distinctIds.Contains(link.SkillId)).ToList();
        if (toRemove.Count > 0)
        {
            dbContext.CandidateProfileSkills.RemoveRange(toRemove);
        }

        var existingSkillIds = existingLinks.Select(link => link.SkillId).ToHashSet();
        var toAdd = distinctIds
            .Where(skillId => !existingSkillIds.Contains(skillId))
            .Select(skillId => new CandidateProfileSkill(candidateProfileId, skillId));
        dbContext.CandidateProfileSkills.AddRange(toAdd);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncLanguagesAsync(
        int candidateProfileId,
        IReadOnlyCollection<int> selectedLanguageIds,
        CancellationToken cancellationToken)
    {
        var distinctIds = selectedLanguageIds.Distinct().ToList();

        var existingLinks = await dbContext.CandidateProfileLanguages
            .Where(link => link.CandidateProfileId == candidateProfileId)
            .ToListAsync(cancellationToken);

        var toRemove = existingLinks.Where(link => !distinctIds.Contains(link.LanguageId)).ToList();
        if (toRemove.Count > 0)
        {
            dbContext.CandidateProfileLanguages.RemoveRange(toRemove);
        }

        var existingLanguageIds = existingLinks.Select(link => link.LanguageId).ToHashSet();
        var toAdd = distinctIds
            .Where(languageId => !existingLanguageIds.Contains(languageId))
            .Select(languageId => new CandidateProfileLanguage(candidateProfileId, languageId));
        dbContext.CandidateProfileLanguages.AddRange(toAdd);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<CandidateProfileFormViewModel> BuildFormWithOptionsAsync(
        CandidateProfileFormViewModel model,
        CancellationToken cancellationToken)
    {
        model.PositionOptions = await dbContext.Positions
            .Where(position => position.IsActive)
            .OrderBy(position => position.Name)
            .Select(position => new SelectOptionViewModel(position.Id, position.Name))
            .ToListAsync(cancellationToken);

        model.SkillOptions = await dbContext.Skills
            .Where(skill => skill.IsActive)
            .OrderBy(skill => skill.Name)
            .Select(skill => new SelectOptionViewModel(skill.Id, skill.Name))
            .ToListAsync(cancellationToken);

        model.LanguageOptions = await dbContext.Languages
            .Where(language => language.IsActive)
            .OrderBy(language => language.Name)
            .Select(language => new SelectOptionViewModel(language.Id, language.Name))
            .ToListAsync(cancellationToken);

        return model;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException &&
            sqlException.Number is 2601 or 2627;
    }
}
