using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.ExperienceRanges;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class ExperienceRangesController(
    ApplicationDbContext dbContext,
    IActivityLogService activityLogService) : Controller
{
    private const string DuplicateNameMessage =
        "Bu deneyim aralığı adı zaten kullanılıyor.";

    private const string OverlappingRangeMessage =
        "Bu aralık, mevcut aktif bir deneyim aralığıyla çakışıyor.";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var experienceRanges = await dbContext.ExperienceRanges
            .OrderBy(experienceRange => experienceRange.MinYears)
            .Select(
                experienceRange => new ExperienceRangeListItemViewModel(
                    experienceRange.Id,
                    experienceRange.Name,
                    experienceRange.MinYears,
                    experienceRange.MaxYears,
                    experienceRange.IsActive))
            .ToListAsync(cancellationToken);

        return View(new ExperienceRangeIndexViewModel(experienceRanges));
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new ExperienceRangeFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        ExperienceRangeFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        ExperienceRange experienceRange;
        try
        {
            experienceRange = new ExperienceRange(
                model.Name,
                model.MinYears!.Value,
                model.MaxYears!.Value);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }

        if (await OverlapsWithActiveRangeAsync(
                experienceRange.MinYears,
                experienceRange.MaxYears,
                excludeId: null,
                cancellationToken))
        {
            ModelState.AddModelError(string.Empty, OverlappingRangeMessage);
            return View(model);
        }

        dbContext.ExperienceRanges.Add(experienceRange);

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
                "Deneyim aralığı oluşturuldu.",
                ActivityEntityTypes.ExperienceRange,
                experienceRange.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var experienceRange = await dbContext.ExperienceRanges.FindAsync(
            [id],
            cancellationToken);

        if (experienceRange is null)
        {
            return NotFound();
        }

        return View(
            new ExperienceRangeFormViewModel
            {
                Id = experienceRange.Id,
                Name = experienceRange.Name,
                MinYears = experienceRange.MinYears,
                MaxYears = experienceRange.MaxYears
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        ExperienceRangeFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var experienceRange = await dbContext.ExperienceRanges.FindAsync(
            [id],
            cancellationToken);

        if (experienceRange is null)
        {
            return NotFound();
        }

        try
        {
            experienceRange.Rename(model.Name);
            experienceRange.ChangeRange(model.MinYears!.Value, model.MaxYears!.Value);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }

        if (await OverlapsWithActiveRangeAsync(
                experienceRange.MinYears,
                experienceRange.MaxYears,
                excludeId: experienceRange.Id,
                cancellationToken))
        {
            ModelState.AddModelError(string.Empty, OverlappingRangeMessage);
            return View(model);
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityUpdated,
                "Deneyim aralığı güncellendi.",
                ActivityEntityTypes.ExperienceRange,
                experienceRange.Id.ToString()));

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
        var experienceRange = await dbContext.ExperienceRanges.FindAsync(
            [id],
            cancellationToken);

        if (experienceRange is null)
        {
            return NotFound();
        }

        experienceRange.Deactivate();

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "Deneyim aralığı pasife alındı.",
                ActivityEntityTypes.ExperienceRange,
                experienceRange.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        var experienceRange = await dbContext.ExperienceRanges.FindAsync(
            [id],
            cancellationToken);

        if (experienceRange is null)
        {
            return NotFound();
        }

        if (await OverlapsWithActiveRangeAsync(
                experienceRange.MinYears,
                experienceRange.MaxYears,
                excludeId: experienceRange.Id,
                cancellationToken))
        {
            ModelState.AddModelError(string.Empty, OverlappingRangeMessage);
            return View(
                "Edit",
                new ExperienceRangeFormViewModel
                {
                    Id = experienceRange.Id,
                    Name = experienceRange.Name,
                    MinYears = experienceRange.MinYears,
                    MaxYears = experienceRange.MaxYears
                });
        }

        experienceRange.Activate();

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "Deneyim aralığı aktifleştirildi.",
                ActivityEntityTypes.ExperienceRange,
                experienceRange.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> OverlapsWithActiveRangeAsync(
        int minYears,
        int maxYears,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ExperienceRanges
            .Where(existing => existing.IsActive)
            .Where(existing => excludeId == null || existing.Id != excludeId)
            .Where(existing => existing.MinYears <= maxYears && minYears <= existing.MaxYears)
            .AnyAsync(cancellationToken);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException &&
            sqlException.Number is 2601 or 2627;
    }
}
