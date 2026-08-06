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

[Authorize(
    Roles = $"{SystemRoles.Admin},{SystemRoles.RecruitmentSpecialist},{SystemRoles.HiringManager}")]
public sealed class JobPostingsController(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IActivityLogService activityLogService,
    IRecruitmentScopeService scopeService,
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

    private const string InvalidStatusTransitionMessage =
        "Bu durum geçişi tanımlı değil.";

    private const string DeleteNotAllowedMessage =
        "Yalnızca Taslak durumundaki ilanlar silinebilir. Yayımlanmış bir ilanı " +
        "kaldırmak için arşivleyin.";

    [HttpGet]
    public async Task<IActionResult> Index(
        string? status,
        DateOnly? deadlineFrom,
        DateOnly? deadlineTo,
        CancellationToken cancellationToken)
    {
        var scope = await scopeService.GetScopeAsync(User, cancellationToken);
        if (scope is null)
        {
            return Forbid();
        }

        var query = scope.ApplyToJobPostings(dbContext.JobPostings);

        if (!string.IsNullOrWhiteSpace(status) && JobPostingStatuses.IsDefined(status))
        {
            query = query.Where(jobPosting => jobPosting.Status == status);
        }

        if (deadlineFrom.HasValue)
        {
            query = query.Where(jobPosting => jobPosting.ApplicationDeadline >= deadlineFrom.Value);
        }

        if (deadlineTo.HasValue)
        {
            query = query.Where(jobPosting => jobPosting.ApplicationDeadline <= deadlineTo.Value);
        }

        var jobPostings = await query
            .OrderBy(jobPosting => jobPosting.Title)
            .Select(
                jobPosting => new JobPostingListItemViewModel(
                    jobPosting.Id,
                    jobPosting.Title,
                    jobPosting.Position.Name,
                    jobPosting.Position.Department.Name,
                    jobPosting.ResponsibleUser.UserName!,
                    jobPosting.ApplicationDeadline,
                    jobPosting.Status))
            .ToListAsync(cancellationToken);

        return View(
            new JobPostingIndexViewModel(
                jobPostings,
                JobPostingStatuses.All.ToList(),
                status,
                deadlineFrom,
                deadlineTo));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var jobPosting = await dbContext.JobPostings
            .Include(entity => entity.Position)
            .ThenInclude(position => position.Department)
            .Include(entity => entity.ResponsibleUser)
            .FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (jobPosting is null)
        {
            return NotFound();
        }

        var scope = await scopeService.GetScopeAsync(User, cancellationToken);
        if (scope is null || !scope.Includes(jobPosting))
        {
            return NotFound();
        }

        return View(
            new JobPostingDetailViewModel(
                jobPosting.Id,
                jobPosting.Title,
                jobPosting.Description,
                jobPosting.Position.Name,
                jobPosting.Position.Department.Name,
                jobPosting.ResponsibleUser.UserName!,
                jobPosting.ApplicationDeadline,
                jobPosting.Status));
    }

    [Authorize(Roles = $"{SystemRoles.Admin},{SystemRoles.RecruitmentSpecialist}")]
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        return View(await BuildFormWithOptionsAsync(new JobPostingFormViewModel(), cancellationToken));
    }

    [Authorize(Roles = $"{SystemRoles.Admin},{SystemRoles.RecruitmentSpecialist}")]
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
                today,
                model.IsInternal);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(await BuildFormWithOptionsAsync(model, cancellationToken));
        }

        dbContext.JobPostings.Add(jobPosting);
        await dbContext.SaveChangesAsync(cancellationToken);

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityCreated,
                "İlan taslak olarak oluşturuldu.",
                ActivityEntityTypes.JobPosting,
                jobPosting.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = $"{SystemRoles.Admin},{SystemRoles.RecruitmentSpecialist}")]
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
            IsInternal = jobPosting.IsInternal,
            RowVersion = Convert.ToBase64String(jobPosting.RowVersion)
        };

        return View(await BuildFormWithOptionsAsync(model, cancellationToken));
    }

    [Authorize(Roles = $"{SystemRoles.Admin},{SystemRoles.RecruitmentSpecialist}")]
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
                today,
                model.IsInternal);
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

    [Authorize(Roles = $"{SystemRoles.Admin},{SystemRoles.RecruitmentSpecialist}")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(
        int id,
        string newStatus,
        CancellationToken cancellationToken)
    {
        var jobPosting = await dbContext.JobPostings.FindAsync([id], cancellationToken);

        if (jobPosting is null)
        {
            return NotFound();
        }

        if (!User.IsInRole(SystemRoles.Admin))
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser is null || jobPosting.ResponsibleUserId != currentUser.Id)
            {
                return Forbid();
            }
        }

        if (!JobPostingStatuses.IsDefined(newStatus) ||
            !JobPostingStatuses.IsValidTransition(jobPosting.Status, newStatus))
        {
            return BadRequest(InvalidStatusTransitionMessage);
        }

        var previousStatus = jobPosting.Status;
        jobPosting.ChangeStatus(newStatus);
        await dbContext.SaveChangesAsync(cancellationToken);

        var actionCode = newStatus == JobPostingStatuses.Archived
            ? ActivityActionCodes.EntityArchived
            : ActivityActionCodes.EntityStatusChanged;
        activityLogService.Stage(
            new ActivityLogEntry(
                actionCode,
                $"İlan durumu '{previousStatus}' değerinden '{newStatus}' değerine değiştirildi.",
                ActivityEntityTypes.JobPosting,
                jobPosting.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = $"{SystemRoles.Admin},{SystemRoles.RecruitmentSpecialist}")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var jobPosting = await dbContext.JobPostings.FindAsync([id], cancellationToken);

        if (jobPosting is null)
        {
            return NotFound();
        }

        if (!User.IsInRole(SystemRoles.Admin))
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser is null || jobPosting.ResponsibleUserId != currentUser.Id)
            {
                return Forbid();
            }
        }

        if (jobPosting.Status != JobPostingStatuses.Draft)
        {
            return BadRequest(DeleteNotAllowedMessage);
        }

        dbContext.JobPostings.Remove(jobPosting);
        await dbContext.SaveChangesAsync(cancellationToken);

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityDeleted,
                "Taslak ilan kalıcı olarak silindi.",
                ActivityEntityTypes.JobPosting,
                id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

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
