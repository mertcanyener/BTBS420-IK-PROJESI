using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.JobFamilies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class JobFamiliesController(
    ApplicationDbContext dbContext,
    IActivityLogService activityLogService) : Controller
{
    private const string DuplicateNameMessage =
        "Bu iş ailesi adı zaten kullanılıyor.";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var jobFamilies = await dbContext.JobFamilies
            .OrderBy(jobFamily => jobFamily.Name)
            .Select(
                jobFamily => new JobFamilyListItemViewModel(
                    jobFamily.Id,
                    jobFamily.Name,
                    jobFamily.IsActive))
            .ToListAsync(cancellationToken);

        return View(new JobFamilyIndexViewModel(jobFamilies));
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new JobFamilyFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        JobFamilyFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        JobFamily jobFamily;
        try
        {
            jobFamily = new JobFamily(model.Name);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }

        dbContext.JobFamilies.Add(jobFamily);

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
                "İş ailesi oluşturuldu.",
                ActivityEntityTypes.JobFamily,
                jobFamily.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var jobFamily = await dbContext.JobFamilies.FindAsync([id], cancellationToken);

        if (jobFamily is null)
        {
            return NotFound();
        }

        return View(
            new JobFamilyFormViewModel { Id = jobFamily.Id, Name = jobFamily.Name });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        JobFamilyFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var jobFamily = await dbContext.JobFamilies.FindAsync([id], cancellationToken);

        if (jobFamily is null)
        {
            return NotFound();
        }

        try
        {
            jobFamily.Rename(model.Name);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityUpdated,
                "İş ailesi güncellendi.",
                ActivityEntityTypes.JobFamily,
                jobFamily.Id.ToString()));

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
        var jobFamily = await dbContext.JobFamilies.FindAsync([id], cancellationToken);

        if (jobFamily is null)
        {
            return NotFound();
        }

        jobFamily.Deactivate();

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "İş ailesi pasife alındı.",
                ActivityEntityTypes.JobFamily,
                jobFamily.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        var jobFamily = await dbContext.JobFamilies.FindAsync([id], cancellationToken);

        if (jobFamily is null)
        {
            return NotFound();
        }

        jobFamily.Activate();

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "İş ailesi aktifleştirildi.",
                ActivityEntityTypes.JobFamily,
                jobFamily.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException &&
            sqlException.Number is 2601 or 2627;
    }
}
