using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.JobPostings;
using BTBS420.RecruitmentSystem.Web.ViewModels.Positions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize(Roles = $"{SystemRoles.Admin},{SystemRoles.RecruitmentSpecialist}")]
public sealed class JobPostingsController(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IActivityLogService activityLogService,
    TimeProvider timeProvider) : Controller
{
    private const string InactivePositionMessage =
        "Seçilen pozisyon aktif değil veya bulunamadı.";

    private const string InvalidResponsibleUserMessage =
        "Seçilen sorumlu uzman aktif değil veya İşe Alım Uzmanı rolünde değil.";

    private const string MissingConcurrencyTokenMessage =
        "İlan sürüm bilgisi eksik, lütfen sayfayı yeniden yükleyin.";

    private const string ConcurrencyConflictMessage =
        "Bu ilan siz düzenlerken başka biri tarafından güncellendi. " +
        "Değişiklikler gösterildi, lütfen kontrol edip tekrar kaydedin.";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var jobPostings = await dbContext.JobPostings
            .OrderBy(jobPosting => jobPosting.Title)
            .Select(
                jobPosting => new JobPostingListItemViewModel(
                    jobPosting.Id,
                    jobPosting.Title,
                    jobPosting.Position.Name,
                    jobPosting.ResponsibleUser.UserName!,
                    jobPosting.ApplicationDeadline,
                    jobPosting.Status))
            .ToListAsync(cancellationToken);

        return View(new JobPostingIndexViewModel(jobPostings));
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        return View(await BuildFormWithOptionsAsync(new JobPostingFormViewModel(), cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        JobPostingFormViewModel model,
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

        JobPosting jobPosting;
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        try
        {
            jobPosting = new JobPosting(
                model.Title,
                model.Description,
                model.PositionId!.Value,
                model.ResponsibleUserId!,
                model.ApplicationDeadline!.Value,
                today);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        dbContext.JobPostings.Add(jobPosting);

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityCreated,
                "İlan taslak olarak oluşturuldu.",
                ActivityEntityTypes.JobPosting,
                jobPosting.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var jobPosting = await dbContext.JobPostings.FindAsync([id], cancellationToken);

        if (jobPosting is null)
        {
            return NotFound();
        }

        var model = new JobPostingFormViewModel
        {
            Id = jobPosting.Id,
            Title = jobPosting.Title,
            Description = jobPosting.Description,
            PositionId = jobPosting.PositionId,
            ResponsibleUserId = jobPosting.ResponsibleUserId,
            ApplicationDeadline = jobPosting.ApplicationDeadline,
            RowVersion = Convert.ToBase64String(jobPosting.RowVersion)
        };

        return View(await BuildFormWithOptionsAsync(model, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        JobPostingFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        var jobPosting = await dbContext.JobPostings.FindAsync([id], cancellationToken);

        if (jobPosting is null)
        {
            return NotFound();
        }

        if (!await ValidateAssociationsAsync(model, cancellationToken))
        {
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        if (string.IsNullOrEmpty(model.RowVersion))
        {
            ModelState.AddModelError(string.Empty, MissingConcurrencyTokenMessage);
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        dbContext.Entry(jobPosting).Property(entity => entity.RowVersion).OriginalValue =
            Convert.FromBase64String(model.RowVersion);

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        try
        {
            jobPosting.Edit(
                model.Title,
                model.Description,
                model.PositionId!.Value,
                model.ResponsibleUserId!,
                model.ApplicationDeadline!.Value,
                today);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityUpdated,
                "İlan güncellendi.",
                ActivityEntityTypes.JobPosting,
                jobPosting.Id.ToString()));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, ConcurrencyConflictMessage);
            model.RowVersion = Convert.ToBase64String(
                (byte[])dbContext.Entry(jobPosting).Property(entity => entity.RowVersion).CurrentValue!);
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> ValidateAssociationsAsync(
        JobPostingFormViewModel model,
        CancellationToken cancellationToken)
    {
        var isValid = true;

        var position = await dbContext.Positions.FindAsync(
            [model.PositionId!.Value],
            cancellationToken);
        if (position is null || !position.IsActive)
        {
            ModelState.AddModelError(nameof(model.PositionId), InactivePositionMessage);
            isValid = false;
        }

        var responsibleUser = await userManager.FindByIdAsync(model.ResponsibleUserId!);
        if (responsibleUser is null ||
            !responsibleUser.IsActive ||
            !await userManager.IsInRoleAsync(responsibleUser, SystemRoles.RecruitmentSpecialist))
        {
            ModelState.AddModelError(nameof(model.ResponsibleUserId), InvalidResponsibleUserMessage);
            isValid = false;
        }

        return isValid;
    }

    private async Task<JobPostingFormViewModel> BuildFormWithOptionsAsync(
        JobPostingFormViewModel model,
        CancellationToken cancellationToken)
    {
        model.PositionOptions = await dbContext.Positions
            .Where(position => position.IsActive)
            .OrderBy(position => position.Name)
            .Select(position => new SelectOptionViewModel(position.Id, position.Name))
            .ToListAsync(cancellationToken);

        var recruiters = await userManager.GetUsersInRoleAsync(SystemRoles.RecruitmentSpecialist);
        model.ResponsibleUserOptions = recruiters
            .Where(user => user.IsActive)
            .OrderBy(user => user.UserName, StringComparer.Ordinal)
            .Select(user => new UserOptionViewModel(user.Id, user.UserName ?? user.Email ?? user.Id))
            .ToList();

        return model;
    }
}
