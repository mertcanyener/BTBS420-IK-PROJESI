using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.Departments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class DepartmentsController(
    ApplicationDbContext dbContext,
    IActivityLogService activityLogService) : Controller
{
    private const string DuplicateNameMessage =
        "Bu departman adı zaten kullanılıyor.";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var departments = await dbContext.Departments
            .OrderBy(department => department.Name)
            .Select(
                department => new DepartmentListItemViewModel(
                    department.Id,
                    department.Name,
                    department.IsActive))
            .ToListAsync(cancellationToken);

        return View(new DepartmentIndexViewModel(departments));
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new DepartmentFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        DepartmentFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        Department department;
        try
        {
            department = new Department(model.Name);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }

        dbContext.Departments.Add(department);

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
                "Departman oluşturuldu.",
                ActivityEntityTypes.Department,
                department.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var department = await dbContext.Departments.FindAsync(
            [id],
            cancellationToken);

        if (department is null)
        {
            return NotFound();
        }

        return View(new DepartmentFormViewModel { Id = department.Id, Name = department.Name });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        DepartmentFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var department = await dbContext.Departments.FindAsync([id], cancellationToken);

        if (department is null)
        {
            return NotFound();
        }

        try
        {
            department.Rename(model.Name);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityUpdated,
                "Departman güncellendi.",
                ActivityEntityTypes.Department,
                department.Id.ToString()));

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
        var department = await dbContext.Departments.FindAsync([id], cancellationToken);

        if (department is null)
        {
            return NotFound();
        }

        department.Deactivate();

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "Departman pasife alındı.",
                ActivityEntityTypes.Department,
                department.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        var department = await dbContext.Departments.FindAsync([id], cancellationToken);

        if (department is null)
        {
            return NotFound();
        }

        department.Activate();

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "Departman aktifleştirildi.",
                ActivityEntityTypes.Department,
                department.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException &&
            sqlException.Number is 2601 or 2627;
    }
}
