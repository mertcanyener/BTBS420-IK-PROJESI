using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.Seniorities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class SenioritiesController(
    ApplicationDbContext dbContext,
    IActivityLogService activityLogService) : Controller
{
    private const string DuplicateNameMessage =
        "Bu kıdem adı zaten kullanılıyor.";

    private const string DuplicateRankMessage =
        "Bu sıra numarası zaten kullanılıyor.";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var seniorities = await dbContext.Seniorities
            .OrderBy(seniority => seniority.Rank)
            .Select(
                seniority => new SeniorityListItemViewModel(
                    seniority.Id,
                    seniority.Name,
                    seniority.Rank,
                    seniority.IsActive))
            .ToListAsync(cancellationToken);

        return View(new SeniorityIndexViewModel(seniorities));
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new SeniorityFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        SeniorityFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        Seniority seniority;
        try
        {
            seniority = new Seniority(model.Name, model.Rank!.Value);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }

        dbContext.Seniorities.Add(seniority);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception, out var message))
        {
            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityCreated,
                "Kıdem oluşturuldu.",
                ActivityEntityTypes.Seniority,
                seniority.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var seniority = await dbContext.Seniorities.FindAsync([id], cancellationToken);

        if (seniority is null)
        {
            return NotFound();
        }

        return View(
            new SeniorityFormViewModel
            {
                Id = seniority.Id,
                Name = seniority.Name,
                Rank = seniority.Rank
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        SeniorityFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var seniority = await dbContext.Seniorities.FindAsync([id], cancellationToken);

        if (seniority is null)
        {
            return NotFound();
        }

        try
        {
            seniority.Rename(model.Name);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }

        seniority.ChangeRank(model.Rank!.Value);

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityUpdated,
                "Kıdem güncellendi.",
                ActivityEntityTypes.Seniority,
                seniority.Id.ToString()));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception, out var message))
        {
            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var seniority = await dbContext.Seniorities.FindAsync([id], cancellationToken);

        if (seniority is null)
        {
            return NotFound();
        }

        seniority.Deactivate();

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "Kıdem pasife alındı.",
                ActivityEntityTypes.Seniority,
                seniority.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        var seniority = await dbContext.Seniorities.FindAsync([id], cancellationToken);

        if (seniority is null)
        {
            return NotFound();
        }

        seniority.Activate();

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "Kıdem aktifleştirildi.",
                ActivityEntityTypes.Seniority,
                seniority.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    private bool IsUniqueConstraintViolation(DbUpdateException exception, out string message)
    {
        if (exception.InnerException is SqlException sqlException &&
            sqlException.Number is 2601 or 2627)
        {
            message = sqlException.Message.Contains("UX_Seniorities_Rank")
                ? DuplicateRankMessage
                : DuplicateNameMessage;
            return true;
        }

        message = string.Empty;
        return false;
    }
}
