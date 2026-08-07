using System.Data;
using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.Notifications;
using BTBS420.RecruitmentSystem.Web.ViewModels.ApplicationsPool;
using BTBS420.RecruitmentSystem.Web.ViewModels.Interviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize]
public sealed class InterviewsController(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IActivityLogService activityLogService,
    INotificationPublisher notificationPublisher,
    IRecruitmentScopeService scopeService) : Controller
{
    private const string OperationFailedMessage = "İşlem tamamlanamadı, lütfen tekrar deneyin.";

    private const string ConcurrencyConflictMessage =
        "Bu mülakat siz düzenlerken başka biri tarafından güncellendi. " +
        "Değişiklikler gösterildi, lütfen kontrol edip tekrar kaydedin.";

    private const string MissingConcurrencyTokenMessage =
        "Mülakat sürüm bilgisi eksik, lütfen sayfayı yeniden yükleyin.";

    private const string InterviewUpdatedMessage = "Mülakat güncellendi.";

    private const string InterviewCompletedMessage = "Mülakat tamamlandı olarak işaretlendi.";

    private const string InterviewCancelledMessage = "Mülakat iptal edildi.";

    private const string InterviewPostponedMessage = "Mülakat ertelendi.";

    private const string PostponeRequiresNewTimeMessage =
        "Erteleme için yeni başlangıç ve bitiş zamanı zorunludur.";

    private const string StatusChangeConcurrencyConflictMessage =
        "Bu mülakat sizden önce başka biri tarafından güncellendi, lütfen sayfayı yenileyip tekrar deneyin.";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var query = dbContext.Interviews.AsQueryable();

        if (User.IsInRole(SystemRoles.Candidate))
        {
            var userId = userManager.GetUserId(User);
            if (userId is null)
            {
                return Forbid();
            }

            query = query.Where(
                interview => interview.JobApplication.CandidateProfile.ApplicationUserId == userId);
        }
        else
        {
            var scope = await scopeService.GetScopeAsync(User, cancellationToken);
            if (scope is null)
            {
                return Forbid();
            }

            query = scope.ApplyToInterviews(query);
        }

        var interviews = await query
            .OrderByDescending(interview => interview.StartAtUtc)
            .Select(
                interview => new
                {
                    interview.Id,
                    CandidateFirstName = interview.JobApplication.CandidateProfile.FirstName,
                    CandidateLastName = interview.JobApplication.CandidateProfile.LastName,
                    JobPostingTitle = interview.JobApplication.JobPosting.Title,
                    interview.InterviewType,
                    interview.StartAtUtc,
                    interview.EndAtUtc,
                    interview.Status
                })
            .ToListAsync(cancellationToken);

        var listItems = interviews
            .Select(
                interview => new InterviewListItemViewModel(
                    interview.Id,
                    $"{interview.CandidateFirstName} {interview.CandidateLastName}",
                    interview.JobPostingTitle,
                    InterviewTypes.GetDisplayLabel(interview.InterviewType),
                    interview.StartAtUtc,
                    interview.EndAtUtc,
                    InterviewStatuses.GetDisplayLabel(interview.Status)))
            .ToList();

        return View(new InterviewIndexViewModel(listItems));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var interview = await LoadInterviewWithScopeAsync(id, cancellationToken);
        if (interview is null)
        {
            return NotFound();
        }

        var participantNames = await dbContext.InterviewParticipants
            .Where(participant => participant.InterviewId == interview.Id)
            .Select(participant => participant.ParticipantUser.UserName)
            .ToListAsync(cancellationToken);

        var canManageInterview = User.IsInRole(SystemRoles.Admin) || User.IsInRole(SystemRoles.RecruitmentSpecialist);
        var canEdit = canManageInterview && interview.Status == InterviewStatuses.Scheduled;
        var canComplete = canManageInterview && InterviewStatuses.IsValidTransition(interview.Status, InterviewStatuses.Completed);
        var canCancel = canManageInterview && InterviewStatuses.IsValidTransition(interview.Status, InterviewStatuses.Cancelled);
        var canViewEvaluationSummary = !User.IsInRole(SystemRoles.Candidate);

        IReadOnlyList<InterviewEvaluationSummaryItemViewModel> evaluationSummary = [];
        double? averageCompetencyScore = null;
        double? averageOverallScore = null;

        if (canViewEvaluationSummary)
        {
            var evaluations = await dbContext.InterviewEvaluations
                .Where(evaluation => evaluation.InterviewId == interview.Id)
                .Join(
                    dbContext.Users,
                    evaluation => evaluation.EvaluatorUserId,
                    user => user.Id,
                    (evaluation, user) => new
                    {
                        EvaluatorName = user.UserName,
                        evaluation.Note,
                        evaluation.CompetencyScore,
                        evaluation.OverallScore,
                        evaluation.Recommendation
                    })
                .ToListAsync(cancellationToken);

            evaluationSummary = evaluations
                .Select(
                    evaluation => new InterviewEvaluationSummaryItemViewModel(
                        evaluation.EvaluatorName ?? string.Empty,
                        evaluation.Note,
                        evaluation.CompetencyScore,
                        evaluation.OverallScore,
                        InterviewEvaluationRecommendations.GetDisplayLabel(evaluation.Recommendation)))
                .ToList();

            if (evaluations.Count > 0)
            {
                averageCompetencyScore = evaluations.Average(evaluation => evaluation.CompetencyScore);
                averageOverallScore = evaluations.Average(evaluation => evaluation.OverallScore);
            }
        }

        var model = new InterviewDetailViewModel(
            interview.Id,
            $"{interview.JobApplication.CandidateProfile.FirstName} {interview.JobApplication.CandidateProfile.LastName}",
            interview.JobApplication.JobPosting.Title,
            interview.JobApplication.JobPosting.Position.Name,
            interview.JobApplication.JobPosting.Position.Department.Name,
            InterviewTypes.GetDisplayLabel(interview.InterviewType),
            interview.StartAtUtc,
            interview.EndAtUtc,
            interview.OnlineMeetingLink,
            interview.Location,
            InterviewStatuses.GetDisplayLabel(interview.Status),
            participantNames.Select(name => name ?? string.Empty).ToList(),
            canEdit,
            canViewEvaluationSummary,
            evaluationSummary,
            averageCompetencyScore,
            averageOverallScore,
            canComplete,
            canCancel);

        return View(model);
    }

    [Authorize(Roles = $"{SystemRoles.Admin},{SystemRoles.RecruitmentSpecialist}")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var interview = await LoadInterviewWithScopeAsync(id, cancellationToken);
        if (interview is null)
        {
            return NotFound();
        }

        var model = new InterviewEditFormViewModel
        {
            Id = interview.Id,
            InterviewType = interview.InterviewType,
            StartAtUtc = interview.StartAtUtc,
            EndAtUtc = interview.EndAtUtc,
            OnlineMeetingLink = interview.OnlineMeetingLink,
            Location = interview.Location,
            RowVersion = Convert.ToBase64String(interview.RowVersion),
            InterviewTypeOptions = BuildInterviewTypeOptions()
        };

        return View(model);
    }

    [Authorize(Roles = $"{SystemRoles.Admin},{SystemRoles.RecruitmentSpecialist}")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        InterviewEditFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.InterviewTypeOptions = BuildInterviewTypeOptions();
            return View(model);
        }

        var interview = await LoadInterviewWithScopeAsync(id, cancellationToken);
        if (interview is null)
        {
            return NotFound();
        }

        if (string.IsNullOrEmpty(model.RowVersion))
        {
            ModelState.AddModelError(string.Empty, MissingConcurrencyTokenMessage);
            model.InterviewTypeOptions = BuildInterviewTypeOptions();
            return View(model);
        }

        dbContext.Entry(interview).Property(entity => entity.RowVersion).OriginalValue =
            Convert.FromBase64String(model.RowVersion);

        var timeChanged =
            interview.StartAtUtc != model.StartAtUtc!.Value || interview.EndAtUtc != model.EndAtUtc!.Value;

        try
        {
            interview.Edit(
                model.InterviewType,
                model.StartAtUtc!.Value,
                model.EndAtUtc!.Value,
                model.OnlineMeetingLink,
                model.Location);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            model.InterviewTypeOptions = BuildInterviewTypeOptions();
            return View(model);
        }

        if (!timeChanged)
        {
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return await HandleConcurrencyConflictAsync(model, cancellationToken);
            }

            TempData["StatusMessage"] = InterviewUpdatedMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var conflictingUserNames = await FindConflictingParticipantUserNamesAsync(
                interview,
                interview.StartAtUtc,
                interview.EndAtUtc,
                cancellationToken);

            if (conflictingUserNames.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);

                ModelState.AddModelError(
                    string.Empty,
                    $"Yeni zaman aralığı şu katılımcılarla çakışıyor: {string.Join(", ", conflictingUserNames)}");
                model.InterviewTypeOptions = BuildInterviewTypeOptions();
                return View(model);
            }

            activityLogService.Stage(
                new ActivityLogEntry(
                    ActivityActionCodes.EntityUpdated,
                    "Mülakat zamanı güncellendi.",
                    ActivityEntityTypes.Interview,
                    interview.Id.ToString(),
                    JobPostingId: interview.JobApplication.JobPostingId.ToString(),
                    CandidateId: interview.JobApplication.CandidateProfile.ApplicationUserId));

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await HandleConcurrencyConflictAsync(model, cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateException or SqlException)
        {
            await transaction.RollbackAsync(cancellationToken);
            ModelState.AddModelError(string.Empty, OperationFailedMessage);
            model.InterviewTypeOptions = BuildInterviewTypeOptions();
            return View(model);
        }

        TempData["StatusMessage"] = InterviewUpdatedMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = $"{SystemRoles.Admin},{SystemRoles.RecruitmentSpecialist}")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, CancellationToken cancellationToken)
    {
        var interview = await LoadInterviewWithScopeAsync(id, cancellationToken);
        if (interview is null)
        {
            return NotFound();
        }

        try
        {
            interview.Complete();
        }
        catch (InvalidOperationException exception)
        {
            TempData["StatusMessage"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                InterviewCompletedMessage,
                ActivityEntityTypes.Interview,
                interview.Id.ToString(),
                JobPostingId: interview.JobApplication.JobPostingId.ToString(),
                CandidateId: interview.JobApplication.CandidateProfile.ApplicationUserId));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["StatusMessage"] = StatusChangeConcurrencyConflictMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["StatusMessage"] = InterviewCompletedMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = $"{SystemRoles.Admin},{SystemRoles.RecruitmentSpecialist}")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var interview = await LoadInterviewWithScopeAsync(id, cancellationToken);
        if (interview is null)
        {
            return NotFound();
        }

        try
        {
            interview.Cancel();
        }
        catch (InvalidOperationException exception)
        {
            TempData["StatusMessage"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                InterviewCancelledMessage,
                ActivityEntityTypes.Interview,
                interview.Id.ToString(),
                JobPostingId: interview.JobApplication.JobPostingId.ToString(),
                CandidateId: interview.JobApplication.CandidateProfile.ApplicationUserId));

        var cancelRecipients = await GetInterviewNotificationRecipientsAsync(
            interview.JobApplication.CandidateProfile.ApplicationUserId, interview.Id, cancellationToken);
        await StageInterviewNotificationAsync(
            cancelRecipients,
            $"interview-cancelled:{interview.Id}",
            "Mülakat iptal edildi",
            $"{interview.StartAtUtc:dd.MM.yyyy HH:mm} tarihli mülakatınız iptal edildi.",
            cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["StatusMessage"] = StatusChangeConcurrencyConflictMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["StatusMessage"] = InterviewCancelledMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = $"{SystemRoles.Admin},{SystemRoles.RecruitmentSpecialist}")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Postpone(
        int id,
        DateTime? newStartAtUtc,
        DateTime? newEndAtUtc,
        CancellationToken cancellationToken)
    {
        var interview = await LoadInterviewWithScopeAsync(id, cancellationToken);
        if (interview is null)
        {
            return NotFound();
        }

        if (newStartAtUtc is null || newEndAtUtc is null)
        {
            TempData["StatusMessage"] = PostponeRequiresNewTimeMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var conflictingUserNames = await FindConflictingParticipantUserNamesAsync(
                interview,
                newStartAtUtc.Value,
                newEndAtUtc.Value,
                cancellationToken);

            if (conflictingUserNames.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                TempData["StatusMessage"] =
                    $"Yeni zaman aralığı şu katılımcılarla çakışıyor: {string.Join(", ", conflictingUserNames)}";
                return RedirectToAction(nameof(Details), new { id });
            }

            interview.Postpone(newStartAtUtc.Value, newEndAtUtc.Value);

            activityLogService.Stage(
                new ActivityLogEntry(
                    ActivityActionCodes.EntityStatusChanged,
                    InterviewPostponedMessage,
                    ActivityEntityTypes.Interview,
                    interview.Id.ToString(),
                    JobPostingId: interview.JobApplication.JobPostingId.ToString(),
                    CandidateId: interview.JobApplication.CandidateProfile.ApplicationUserId));

            var postponeRecipients = await GetInterviewNotificationRecipientsAsync(
                interview.JobApplication.CandidateProfile.ApplicationUserId, interview.Id, cancellationToken);
            await StageInterviewNotificationAsync(
                postponeRecipients,
                $"interview-postponed:{interview.Id}:{newStartAtUtc.Value.Ticks}",
                "Mülakat ertelendi",
                $"Mülakatınızın yeni zamanı {newStartAtUtc.Value:dd.MM.yyyy HH:mm} olarak güncellendi.",
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            await transaction.RollbackAsync(cancellationToken);
            TempData["StatusMessage"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            TempData["StatusMessage"] = StatusChangeConcurrencyConflictMessage;
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception exception) when (exception is DbUpdateException or SqlException)
        {
            await transaction.RollbackAsync(cancellationToken);
            TempData["StatusMessage"] = OperationFailedMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["StatusMessage"] = InterviewPostponedMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<IReadOnlyList<string>> FindConflictingParticipantUserNamesAsync(
        Interview interview,
        DateTime newStartAtUtc,
        DateTime newEndAtUtc,
        CancellationToken cancellationToken)
    {
        var participantIds = await dbContext.InterviewParticipants
            .Where(participant => participant.InterviewId == interview.Id)
            .Select(participant => participant.ParticipantUserId)
            .ToListAsync(cancellationToken);

        var conflictingUserIds = new List<string>();
        foreach (var participantId in participantIds)
        {
            var hasConflict = await dbContext.InterviewParticipants
                .Where(
                    participant =>
                        participant.ParticipantUserId == participantId &&
                        participant.InterviewId != interview.Id &&
                        participant.Interview.Status != InterviewStatuses.Cancelled &&
                        participant.Interview.StartAtUtc < newEndAtUtc &&
                        participant.Interview.EndAtUtc > newStartAtUtc)
                .AnyAsync(cancellationToken);

            if (hasConflict)
            {
                conflictingUserIds.Add(participantId);
            }
        }

        if (conflictingUserIds.Count == 0)
        {
            return [];
        }

        return await dbContext.Users
            .Where(user => conflictingUserIds.Contains(user.Id))
            .Select(user => user.UserName!)
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<string>> GetInterviewNotificationRecipientsAsync(
        string candidateUserId,
        int interviewId,
        CancellationToken cancellationToken)
    {
        var participantIds = await dbContext.InterviewParticipants
            .Where(participant => participant.InterviewId == interviewId)
            .Select(participant => participant.ParticipantUserId)
            .ToListAsync(cancellationToken);

        var recipients = new List<string> { candidateUserId };
        recipients.AddRange(participantIds);

        return recipients.Distinct(StringComparer.Ordinal).ToList();
    }

    private async Task StageInterviewNotificationAsync(
        IEnumerable<string> recipientUserIds,
        string eventKey,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        foreach (var recipientUserId in recipientUserIds)
        {
            await notificationPublisher.StageIfMissingAsync(
                new NotificationEntry(recipientUserId, eventKey, title, message),
                cancellationToken);
        }
    }

    private async Task<IActionResult> HandleConcurrencyConflictAsync(
        InterviewEditFormViewModel model,
        CancellationToken cancellationToken)
    {
        var currentInterview = await dbContext.Interviews
            .AsNoTracking()
            .FirstOrDefaultAsync(interview => interview.Id == model.Id, cancellationToken);

        ModelState.AddModelError(string.Empty, ConcurrencyConflictMessage);
        model.InterviewTypeOptions = BuildInterviewTypeOptions();
        if (currentInterview is not null)
        {
            model.RowVersion = Convert.ToBase64String(currentInterview.RowVersion);
        }

        return View(nameof(Edit), model);
    }

    private static IReadOnlyList<InterviewTypeOptionViewModel> BuildInterviewTypeOptions()
    {
        return InterviewTypes.All
            .Select(type => new InterviewTypeOptionViewModel(type, InterviewTypes.GetDisplayLabel(type)))
            .OrderBy(option => option.Label, StringComparer.Ordinal)
            .ToList();
    }

    private async Task<Interview?> LoadInterviewWithScopeAsync(int id, CancellationToken cancellationToken)
    {
        var interview = await dbContext.Interviews
            .Include(candidateInterview => candidateInterview.JobApplication)
            .ThenInclude(candidateApplication => candidateApplication.JobPosting)
            .ThenInclude(jobPosting => jobPosting.Position)
            .ThenInclude(position => position.Department)
            .Include(candidateInterview => candidateInterview.JobApplication)
            .ThenInclude(candidateApplication => candidateApplication.CandidateProfile)
            .FirstOrDefaultAsync(candidateInterview => candidateInterview.Id == id, cancellationToken);

        if (interview is null)
        {
            return null;
        }

        if (User.IsInRole(SystemRoles.Candidate))
        {
            var userId = userManager.GetUserId(User);
            return userId is not null &&
                interview.JobApplication.CandidateProfile.ApplicationUserId == userId
                ? interview
                : null;
        }

        var scope = await scopeService.GetScopeAsync(User, cancellationToken);
        return scope is not null && scope.Includes(interview) ? interview : null;
    }
}
