using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.Positions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class PositionsController(
    ApplicationDbContext dbContext,
    IActivityLogService activityLogService) : Controller
{
    private const string DuplicateNameMessage =
        "Bu departmanda aynı adlı bir pozisyon zaten var.";

    private const string InactiveDepartmentMessage =
        "Seçilen departman aktif değil veya bulunamadı.";

    private const string InactiveJobFamilyMessage =
        "Seçilen iş ailesi aktif değil veya bulunamadı.";

    private const string InactiveSeniorityMessage =
        "Seçilen kıdem aktif değil veya bulunamadı.";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var positions = await dbContext.Positions
            .OrderBy(position => position.Name)
            .Select(
                position => new PositionListItemViewModel(
                    position.Id,
                    position.Name,
                    position.Department.Name,
                    position.JobFamily != null ? position.JobFamily.Name : null,
                    position.Seniority != null ? position.Seniority.Name : null,
                    position.IsActive))
            .ToListAsync(cancellationToken);

        return View(new PositionIndexViewModel(positions));
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        return View(await BuildFormWithOptionsAsync(new PositionFormViewModel(), cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        PositionFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        if (!await ValidateAssociationsAsync(model, cancellationToken))
        {
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        Position position;
        try
        {
            position = new Position(
                model.Name,
                model.DepartmentId!.Value,
                model.JobFamilyId,
                model.SeniorityId);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        dbContext.Positions.Add(position);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            ModelState.AddModelError(string.Empty, DuplicateNameMessage);
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityCreated,
                "Pozisyon oluşturuldu.",
                ActivityEntityTypes.Position,
                position.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var position = await dbContext.Positions.FindAsync([id], cancellationToken);

        if (position is null)
        {
            return NotFound();
        }

        var model = new PositionFormViewModel
        {
            Id = position.Id,
            Name = position.Name,
            DepartmentId = position.DepartmentId,
            JobFamilyId = position.JobFamilyId,
            SeniorityId = position.SeniorityId
        };

        return View(await BuildFormWithOptionsAsync(model, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        PositionFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        var position = await dbContext.Positions.FindAsync([id], cancellationToken);

        if (position is null)
        {
            return NotFound();
        }

        if (!await ValidateAssociationsAsync(model, cancellationToken))
        {
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        try
        {
            position.Rename(model.Name);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        position.ChangeAssociations(
            model.DepartmentId!.Value,
            model.JobFamilyId,
            model.SeniorityId);

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityUpdated,
                "Pozisyon güncellendi.",
                ActivityEntityTypes.Position,
                position.Id.ToString()));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            ModelState.AddModelError(string.Empty, DuplicateNameMessage);
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var position = await dbContext.Positions.FindAsync([id], cancellationToken);

        if (position is null)
        {
            return NotFound();
        }

        position.Deactivate();

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "Pozisyon pasife alındı.",
                ActivityEntityTypes.Position,
                position.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        var position = await dbContext.Positions.FindAsync([id], cancellationToken);

        if (position is null)
        {
            return NotFound();
        }

        position.Activate();

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "Pozisyon aktifleştirildi.",
                ActivityEntityTypes.Position,
                position.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> ValidateAssociationsAsync(
        PositionFormViewModel model,
        CancellationToken cancellationToken)
    {
        var isValid = true;

        var department = await dbContext.Departments.FindAsync(
            [model.DepartmentId!.Value],
            cancellationToken);
        if (department is null || !department.IsActive)
        {
            ModelState.AddModelError(nameof(model.DepartmentId), InactiveDepartmentMessage);
            isValid = false;
        }

        if (model.JobFamilyId.HasValue)
        {
            var jobFamily = await dbContext.JobFamilies.FindAsync(
                [model.JobFamilyId.Value],
                cancellationToken);
            if (jobFamily is null || !jobFamily.IsActive)
            {
                ModelState.AddModelError(nameof(model.JobFamilyId), InactiveJobFamilyMessage);
                isValid = false;
            }
        }

        if (model.SeniorityId.HasValue)
        {
            var seniority = await dbContext.Seniorities.FindAsync(
                [model.SeniorityId.Value],
                cancellationToken);
            if (seniority is null || !seniority.IsActive)
            {
                ModelState.AddModelError(nameof(model.SeniorityId), InactiveSeniorityMessage);
                isValid = false;
            }
        }

        return isValid;
    }

    private async Task<PositionFormViewModel> BuildFormWithOptionsAsync(
        PositionFormViewModel model,
        CancellationToken cancellationToken)
    {
        model.DepartmentOptions = await dbContext.Departments
            .Where(department => department.IsActive)
            .OrderBy(department => department.Name)
            .Select(department => new SelectOptionViewModel(department.Id, department.Name))
            .ToListAsync(cancellationToken);

        model.JobFamilyOptions = await dbContext.JobFamilies
            .Where(jobFamily => jobFamily.IsActive)
            .OrderBy(jobFamily => jobFamily.Name)
            .Select(jobFamily => new SelectOptionViewModel(jobFamily.Id, jobFamily.Name))
            .ToListAsync(cancellationToken);

        model.SeniorityOptions = await dbContext.Seniorities
            .Where(seniority => seniority.IsActive)
            .OrderBy(seniority => seniority.Rank)
            .Select(seniority => new SelectOptionViewModel(seniority.Id, seniority.Name))
            .ToListAsync(cancellationToken);

        return model;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException &&
            sqlException.Number is 2601 or 2627;
    }
}
