using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.Educations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class EducationsController(
    ApplicationDbContext dbContext,
    IActivityLogService activityLogService) : Controller
{
    private const string DuplicateNameMessage = "Bu eğitim adı zaten kullanılıyor.";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var educations = await dbContext.Educations
            .OrderBy(education => education.Name)
            .Select(education => new EducationListItemViewModel(education.Id, education.Name, education.IsActive))
            .ToListAsync(cancellationToken);

        return View(new EducationIndexViewModel(educations));
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new EducationFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        EducationFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        Education education;
        try
        {
            education = new Education(model.Name);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }

        dbContext.Educations.Add(education);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            ModelState.AddModelError(string.Empty, DuplicateNameMessage);
            return View(model);
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityCreated,
                "Eğitim oluşturuldu.",
                ActivityEntityTypes.Education,
                education.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var education = await dbContext.Educations.FindAsync([id], cancellationToken);

        if (education is null)
        {
            return NotFound();
        }

        return View(new EducationFormViewModel { Id = education.Id, Name = education.Name });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        EducationFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var education = await dbContext.Educations.FindAsync([id], cancellationToken);

        if (education is null)
        {
            return NotFound();
        }

        try
        {
            education.Rename(model.Name);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityUpdated,
                "Eğitim güncellendi.",
                ActivityEntityTypes.Education,
                education.Id.ToString()));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            ModelState.AddModelError(string.Empty, DuplicateNameMessage);
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var education = await dbContext.Educations.FindAsync([id], cancellationToken);

        if (education is null)
        {
            return NotFound();
        }

        education.Deactivate();

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "Eğitim pasife alındı.",
                ActivityEntityTypes.Education,
                education.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        var education = await dbContext.Educations.FindAsync([id], cancellationToken);

        if (education is null)
        {
            return NotFound();
        }

        education.Activate();

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "Eğitim aktifleştirildi.",
                ActivityEntityTypes.Education,
                education.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException &&
            sqlException.Number is 2601 or 2627;
    }
}
