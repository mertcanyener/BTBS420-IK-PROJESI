using System.Data;
using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.Notifications;
using BTBS420.RecruitmentSystem.Web.Storage;
using BTBS420.RecruitmentSystem.Web.ViewModels.ApplicationsPool;
using BTBS420.RecruitmentSystem.Web.ViewModels.Dashboard;
using BTBS420.RecruitmentSystem.Web.ViewModels.Positions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.RecruitmentStaffOnly)]
public sealed class ApplicationsPoolController(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IActivityLogService activityLogService,
    INotificationPublisher notificationPublisher,
    ICandidateDocumentStorageService storageService,
    IRecruitmentScopeService scopeService,
    TimeProvider timeProvider) : Controller
{
    private const int DefaultPageSize = 10;

    private const int MaximumPageSize = 50;

    private const string NoteRequiredMessage = "Not metni boş olamaz.";

    private const string NoteAddedMessage = "Not eklendi.";

    private const string InterviewScheduledMessage = "Mülakat planlandı.";

    private const string ParticipantRequiredMessage = "En az bir katılımcı seçmelisiniz.";

    private const string ParticipantsAlreadyAssignedMessage =
        "Seçilen katılımcılar zaten bu mülakata atanmış.";

    private const string ParticipantsAssignedMessage = "Katılımcılar atandı.";

    private const string OperationFailedMessage = "İşlem tamamlanamadı, lütfen tekrar deneyin.";

    private const string ApplicationRejectedMessage = "Başvuru reddedildi.";

    private const string ApplicationReevaluatedMessage = "Başvuru yeniden değerlendirmeye alındı.";

    private const string ApplicationArchivedMessage = "Başvuru arşivlendi.";

    private const string ApplicationMovedToScreeningMessage = "Başvuru ön elemeye alındı.";

    private const string ApplicationMovedToInterviewMessage = "Başvuru mülakat aşamasına alındı.";

    private const string ReevaluateReasonRequiredMessage =
        "Yeniden değerlendirme için bir gerekçe belirtmelisiniz.";

    private const string StatusChangeConcurrencyConflictMessage =
        "Bu başvuru siz işlem yaparken başka biri tarafından güncellendi, lütfen tekrar deneyin.";

    private static readonly string[] InternalRoleNames =
    [
        SystemRoles.Admin,
        SystemRoles.RecruitmentSpecialist,
        SystemRoles.HiringManager
    ];

    [HttpGet]
    public async Task<IActionResult> Index(
        string? status,
        int? departmentId,
        int? positionId,
        int? jobPostingId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        int page = 1,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);

        var scope = await scopeService.GetScopeAsync(User, cancellationToken);
        if (scope is null)
        {
            return Forbid();
        }

        var query = scope.ApplyToJobApplications(dbContext.JobApplications);

        if (!string.IsNullOrWhiteSpace(status) && ApplicationStatuses.IsDefined(status))
        {
            query = query.Where(application => application.Status == status);
        }

        if (departmentId.HasValue)
        {
            query = query.Where(
                application => application.JobPosting.Position.DepartmentId == departmentId.Value);
        }

        if (positionId.HasValue)
        {
            query = query.Where(application => application.JobPosting.PositionId == positionId.Value);
        }

        if (jobPostingId.HasValue)
        {
            query = query.Where(application => application.JobPostingId == jobPostingId.Value);
        }

        if (dateFrom.HasValue)
        {
            var fromUtc = dateFrom.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(application => application.AppliedAtUtc >= fromUtc);
        }

        if (dateTo.HasValue)
        {
            var toUtc = dateTo.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(application => application.AppliedAtUtc <= toUtc);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var applications = await query
            .OrderByDescending(application => application.AppliedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(
                application => new
                {
                    application.Id,
                    application.CandidateProfile.FirstName,
                    application.CandidateProfile.LastName,
                    JobPostingTitle = application.JobPosting.Title,
                    PositionName = application.JobPosting.Position.Name,
                    DepartmentName = application.JobPosting.Position.Department.Name,
                    application.Status,
                    application.AppliedAtUtc
                })
            .ToListAsync(cancellationToken);

        var listItems = applications
            .Select(
                application => new ApplicationPoolListItemViewModel(
                    application.Id,
                    $"{application.FirstName} {application.LastName}",
                    application.JobPostingTitle,
                    application.PositionName,
                    application.DepartmentName,
                    ApplicationStatuses.GetDisplayLabel(application.Status),
                    application.AppliedAtUtc))
            .ToList();

        var filterOptions = await BuildFilterOptionsAsync(scope, cancellationToken);
        var filter = new DashboardFilterViewModel(
            status, departmentId, positionId, jobPostingId, dateFrom, dateTo, page, pageSize);

        return View(new ApplicationPoolIndexViewModel(listItems, filter, filterOptions, totalCount));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var application = await dbContext.JobApplications
            .Include(candidateApplication => candidateApplication.JobPosting)
            .ThenInclude(jobPosting => jobPosting.Position)
            .ThenInclude(position => position.Department)
            .Include(candidateApplication => candidateApplication.CandidateProfile)
            .ThenInclude(profile => profile.TargetPosition)
            .FirstOrDefaultAsync(candidateApplication => candidateApplication.Id == id, cancellationToken);

        if (application is null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedForApplicationAsync(application.JobPosting))
        {
            return NotFound();
        }

        var model = await BuildDetailViewModelAsync(application, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNote(int id, string body, CancellationToken cancellationToken)
    {
        var application = await dbContext.JobApplications
            .Include(candidateApplication => candidateApplication.JobPosting)
            .ThenInclude(jobPosting => jobPosting.Position)
            .FirstOrDefaultAsync(candidateApplication => candidateApplication.Id == id, cancellationToken);

        if (application is null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedForApplicationAsync(application.JobPosting))
        {
            return NotFound();
        }

        var actorUserId = userManager.GetUserId(User);
        if (actorUserId is null)
        {
            return Forbid();
        }

        ApplicationNote note;
        try
        {
            note = new ApplicationNote(
                application.Id,
                actorUserId,
                body,
                timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (ArgumentException)
        {
            TempData["StatusMessage"] = NoteRequiredMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        dbContext.ApplicationNotes.Add(note);
        await dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = NoteAddedMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? reason, CancellationToken cancellationToken)
    {
        var application = await dbContext.JobApplications
            .Include(candidateApplication => candidateApplication.JobPosting)
            .ThenInclude(jobPosting => jobPosting.Position)
            .Include(candidateApplication => candidateApplication.CandidateProfile)
            .FirstOrDefaultAsync(candidateApplication => candidateApplication.Id == id, cancellationToken);

        if (application is null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedForApplicationAsync(application.JobPosting))
        {
            return NotFound();
        }

        var actorUserId = userManager.GetUserId(User);
        if (actorUserId is null)
        {
            return Forbid();
        }

        JobApplicationStatusChange statusChange;
        try
        {
            statusChange = application.TransitionTo(
                ApplicationStatuses.Rejected,
                actorUserId,
                reason,
                timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (InvalidOperationException exception)
        {
            TempData["StatusMessage"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        dbContext.JobApplicationStatusChanges.Add(statusChange);

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "Personel başvuruyu reddetti.",
                ActivityEntityTypes.Application,
                application.Id.ToString(),
                JobPostingId: application.JobPostingId.ToString(),
                CandidateId: application.CandidateProfile.ApplicationUserId));

        await StageStatusChangeNotificationAsync(application, statusChange, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["StatusMessage"] = StatusChangeConcurrencyConflictMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["StatusMessage"] = ApplicationRejectedMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reevaluate(int id, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["StatusMessage"] = ReevaluateReasonRequiredMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        var application = await dbContext.JobApplications
            .Include(candidateApplication => candidateApplication.JobPosting)
            .ThenInclude(jobPosting => jobPosting.Position)
            .Include(candidateApplication => candidateApplication.CandidateProfile)
            .FirstOrDefaultAsync(candidateApplication => candidateApplication.Id == id, cancellationToken);

        if (application is null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedForApplicationAsync(application.JobPosting))
        {
            return NotFound();
        }

        var actorUserId = userManager.GetUserId(User);
        if (actorUserId is null)
        {
            return Forbid();
        }

        JobApplicationStatusChange statusChange;
        try
        {
            statusChange = application.TransitionTo(
                ApplicationStatuses.Screening,
                actorUserId,
                reason,
                timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (InvalidOperationException exception)
        {
            TempData["StatusMessage"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        dbContext.JobApplicationStatusChanges.Add(statusChange);

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "Personel reddedilmiş başvuruyu yeniden değerlendirmeye aldı.",
                ActivityEntityTypes.Application,
                application.Id.ToString(),
                JobPostingId: application.JobPostingId.ToString(),
                CandidateId: application.CandidateProfile.ApplicationUserId));

        await StageStatusChangeNotificationAsync(application, statusChange, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["StatusMessage"] = StatusChangeConcurrencyConflictMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["StatusMessage"] = ApplicationReevaluatedMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id, CancellationToken cancellationToken)
    {
        var application = await dbContext.JobApplications
            .Include(candidateApplication => candidateApplication.JobPosting)
            .ThenInclude(jobPosting => jobPosting.Position)
            .Include(candidateApplication => candidateApplication.CandidateProfile)
            .FirstOrDefaultAsync(candidateApplication => candidateApplication.Id == id, cancellationToken);

        if (application is null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedForApplicationAsync(application.JobPosting))
        {
            return NotFound();
        }

        try
        {
            application.Archive(timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (InvalidOperationException exception)
        {
            TempData["StatusMessage"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityArchived,
                "Personel başvuruyu arşivledi.",
                ActivityEntityTypes.Application,
                application.Id.ToString(),
                JobPostingId: application.JobPostingId.ToString(),
                CandidateId: application.CandidateProfile.ApplicationUserId));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["StatusMessage"] = StatusChangeConcurrencyConflictMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["StatusMessage"] = ApplicationArchivedMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveToScreening(int id, CancellationToken cancellationToken)
    {
        var application = await dbContext.JobApplications
            .Include(candidateApplication => candidateApplication.JobPosting)
            .ThenInclude(jobPosting => jobPosting.Position)
            .Include(candidateApplication => candidateApplication.CandidateProfile)
            .FirstOrDefaultAsync(candidateApplication => candidateApplication.Id == id, cancellationToken);

        if (application is null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedForApplicationAsync(application.JobPosting))
        {
            return NotFound();
        }

        var actorUserId = userManager.GetUserId(User);
        if (actorUserId is null)
        {
            return Forbid();
        }

        JobApplicationStatusChange statusChange;
        try
        {
            statusChange = application.TransitionTo(
                ApplicationStatuses.Screening,
                actorUserId,
                reason: null,
                timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (InvalidOperationException exception)
        {
            TempData["StatusMessage"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        dbContext.JobApplicationStatusChanges.Add(statusChange);

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "Personel başvuruyu ön elemeye aldı.",
                ActivityEntityTypes.Application,
                application.Id.ToString(),
                JobPostingId: application.JobPostingId.ToString(),
                CandidateId: application.CandidateProfile.ApplicationUserId));

        await StageStatusChangeNotificationAsync(application, statusChange, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["StatusMessage"] = StatusChangeConcurrencyConflictMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["StatusMessage"] = ApplicationMovedToScreeningMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveToInterview(int id, CancellationToken cancellationToken)
    {
        var application = await dbContext.JobApplications
            .Include(candidateApplication => candidateApplication.JobPosting)
            .ThenInclude(jobPosting => jobPosting.Position)
            .Include(candidateApplication => candidateApplication.CandidateProfile)
            .FirstOrDefaultAsync(candidateApplication => candidateApplication.Id == id, cancellationToken);

        if (application is null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedForApplicationAsync(application.JobPosting))
        {
            return NotFound();
        }

        var actorUserId = userManager.GetUserId(User);
        if (actorUserId is null)
        {
            return Forbid();
        }

        JobApplicationStatusChange statusChange;
        try
        {
            statusChange = application.TransitionTo(
                ApplicationStatuses.Interview,
                actorUserId,
                reason: null,
                timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (InvalidOperationException exception)
        {
            TempData["StatusMessage"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        dbContext.JobApplicationStatusChanges.Add(statusChange);

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "Personel başvuruyu mülakat aşamasına aldı.",
                ActivityEntityTypes.Application,
                application.Id.ToString(),
                JobPostingId: application.JobPostingId.ToString(),
                CandidateId: application.CandidateProfile.ApplicationUserId));

        await StageStatusChangeNotificationAsync(application, statusChange, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["StatusMessage"] = StatusChangeConcurrencyConflictMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["StatusMessage"] = ApplicationMovedToInterviewMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> DownloadDocument(
        int id,
        int documentId,
        CancellationToken cancellationToken)
    {
        var application = await dbContext.JobApplications
            .Include(candidateApplication => candidateApplication.JobPosting)
            .ThenInclude(jobPosting => jobPosting.Position)
            .Include(candidateApplication => candidateApplication.CandidateProfile)
            .FirstOrDefaultAsync(candidateApplication => candidateApplication.Id == id, cancellationToken);

        if (application is null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedForApplicationAsync(application.JobPosting))
        {
            return NotFound();
        }

        var document = await dbContext.CandidateDocuments
            .FirstOrDefaultAsync(
                candidateDocument =>
                    candidateDocument.Id == documentId &&
                    candidateDocument.CandidateProfileId == application.CandidateProfileId,
                cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityDownloaded,
                "Yetkili personel başvuru havuzundan aday belgesini indirdi.",
                ActivityEntityTypes.CandidateDocument,
                document.Id.ToString(),
                JobPostingId: application.JobPostingId.ToString(),
                CandidateId: application.CandidateProfile.ApplicationUserId));
        await dbContext.SaveChangesAsync(cancellationToken);

        var stream = storageService.OpenRead(document.CandidateProfileId, document.StoredFileName);
        return File(stream, document.ContentType, document.OriginalFileName);
    }

    [Authorize(Roles = $"{SystemRoles.Admin},{SystemRoles.RecruitmentSpecialist}")]
    [HttpGet]
    public async Task<IActionResult> CreateInterview(int id, CancellationToken cancellationToken)
    {
        var application = await dbContext.JobApplications
            .Include(candidateApplication => candidateApplication.JobPosting)
            .ThenInclude(jobPosting => jobPosting.Position)
            .FirstOrDefaultAsync(candidateApplication => candidateApplication.Id == id, cancellationToken);

        if (application is null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedForApplicationAsync(application.JobPosting))
        {
            return NotFound();
        }

        return View(
            new InterviewFormViewModel { InterviewTypeOptions = BuildInterviewTypeOptions() });
    }

    [Authorize(Roles = $"{SystemRoles.Admin},{SystemRoles.RecruitmentSpecialist}")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInterview(
        int id,
        InterviewFormViewModel model,
        CancellationToken cancellationToken)
    {
        var application = await dbContext.JobApplications
            .Include(candidateApplication => candidateApplication.JobPosting)
            .ThenInclude(jobPosting => jobPosting.Position)
            .Include(candidateApplication => candidateApplication.CandidateProfile)
            .FirstOrDefaultAsync(candidateApplication => candidateApplication.Id == id, cancellationToken);

        if (application is null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedForApplicationAsync(application.JobPosting))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            model.InterviewTypeOptions = BuildInterviewTypeOptions();
            return View(model);
        }

        Interview interview;
        try
        {
            interview = new Interview(
                application.Id,
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

        dbContext.Interviews.Add(interview);
        await dbContext.SaveChangesAsync(cancellationToken);

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityCreated,
                "Mülakat planlandı.",
                ActivityEntityTypes.Interview,
                interview.Id.ToString(),
                JobPostingId: application.JobPostingId.ToString(),
                CandidateId: application.CandidateProfile.ApplicationUserId));

        var recipients = await GetInterviewNotificationRecipientsAsync(
            application.CandidateProfile.ApplicationUserId, interview.Id, cancellationToken);
        await StageInterviewNotificationAsync(
            recipients,
            $"interview-created:{interview.Id}:{interview.StartAtUtc.Ticks}",
            "Mülakat planlandı",
            $"{InterviewTypes.GetDisplayLabel(interview.InterviewType)} türünde mülakatınız " +
            $"{interview.StartAtUtc:dd.MM.yyyy HH:mm} tarihine planlandı.",
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = InterviewScheduledMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = $"{SystemRoles.Admin},{SystemRoles.RecruitmentSpecialist}")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignParticipants(
        int id,
        int interviewId,
        List<string> participantUserIds,
        CancellationToken cancellationToken)
    {
        var interview = await dbContext.Interviews
            .Include(candidateInterview => candidateInterview.JobApplication)
            .ThenInclude(candidateApplication => candidateApplication.JobPosting)
            .ThenInclude(jobPosting => jobPosting.Position)
            .Include(candidateInterview => candidateInterview.JobApplication)
            .ThenInclude(candidateApplication => candidateApplication.CandidateProfile)
            .FirstOrDefaultAsync(
                candidateInterview =>
                    candidateInterview.Id == interviewId &&
                    candidateInterview.JobApplicationId == id,
                cancellationToken);

        if (interview is null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedForApplicationAsync(interview.JobApplication.JobPosting))
        {
            return NotFound();
        }

        var distinctRequestedIds = (participantUserIds ?? [])
            .Where(participantId => !string.IsNullOrWhiteSpace(participantId))
            .Distinct()
            .ToList();

        if (distinctRequestedIds.Count == 0)
        {
            TempData["StatusMessage"] = ParticipantRequiredMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        var alreadyAssignedIds = await dbContext.InterviewParticipants
            .Where(participant => participant.InterviewId == interview.Id)
            .Select(participant => participant.ParticipantUserId)
            .ToListAsync(cancellationToken);

        var newParticipantIds = distinctRequestedIds.Except(alreadyAssignedIds).ToList();

        if (newParticipantIds.Count == 0)
        {
            TempData["StatusMessage"] = ParticipantsAlreadyAssignedMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var conflictingUserIds = new List<string>();
            foreach (var participantId in newParticipantIds)
            {
                var hasConflict = await dbContext.InterviewParticipants
                    .Where(
                        participant =>
                            participant.ParticipantUserId == participantId &&
                            participant.InterviewId != interview.Id &&
                            participant.Interview.Status != InterviewStatuses.Cancelled &&
                            participant.Interview.StartAtUtc < interview.EndAtUtc &&
                            participant.Interview.EndAtUtc > interview.StartAtUtc)
                    .AnyAsync(cancellationToken);

                if (hasConflict)
                {
                    conflictingUserIds.Add(participantId);
                }
            }

            if (conflictingUserIds.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);

                var conflictingUserNames = await dbContext.Users
                    .Where(user => conflictingUserIds.Contains(user.Id))
                    .Select(user => user.UserName)
                    .ToListAsync(cancellationToken);

                TempData["StatusMessage"] =
                    $"Şu katılımcıların çakışan bir mülakatı var: {string.Join(", ", conflictingUserNames)}";
                return RedirectToAction(nameof(Details), new { id });
            }

            var assignedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            foreach (var participantId in newParticipantIds)
            {
                dbContext.InterviewParticipants.Add(
                    new InterviewParticipant(interview.Id, participantId, assignedAtUtc));
            }

            var newParticipantNames = await dbContext.Users
                .Where(user => newParticipantIds.Contains(user.Id))
                .Select(user => user.UserName)
                .ToListAsync(cancellationToken);

            activityLogService.Stage(
                new ActivityLogEntry(
                    ActivityActionCodes.EntityUpdated,
                    $"Mülakata katılımcı atandı: {string.Join(", ", newParticipantNames)}.",
                    ActivityEntityTypes.Interview,
                    interview.Id.ToString(),
                    JobPostingId: interview.JobApplication.JobPostingId.ToString(),
                    CandidateId: interview.JobApplication.CandidateProfile.ApplicationUserId));

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateException or SqlException)
        {
            await transaction.RollbackAsync(cancellationToken);
            TempData["StatusMessage"] = OperationFailedMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["StatusMessage"] = ParticipantsAssignedMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    private static IReadOnlyList<InterviewTypeOptionViewModel> BuildInterviewTypeOptions()
    {
        return InterviewTypes.All
            .Select(type => new InterviewTypeOptionViewModel(type, InterviewTypes.GetDisplayLabel(type)))
            .OrderBy(option => option.Label, StringComparer.Ordinal)
            .ToList();
    }

    private Task StageStatusChangeNotificationAsync(
        JobApplication application,
        JobApplicationStatusChange statusChange,
        CancellationToken cancellationToken)
    {
        var eventKey =
            $"application-status-changed:{application.Id}:{statusChange.ToStatus}:{statusChange.ChangedAtUtc.Ticks}";
        var message =
            $"\"{application.JobPosting.Title}\" ilanı için başvurunuz " +
            $"\"{ApplicationStatuses.GetDisplayLabel(statusChange.FromStatus)}\" durumundan " +
            $"\"{ApplicationStatuses.GetDisplayLabel(statusChange.ToStatus)}\" durumuna güncellendi.";

        return notificationPublisher.StageIfMissingAsync(
            new NotificationEntry(
                application.CandidateProfile.ApplicationUserId,
                eventKey,
                "Başvuru durumunuz güncellendi",
                message),
            cancellationToken);
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

    private async Task<bool> IsAuthorizedForApplicationAsync(JobPosting jobPosting)
    {
        var scope = await scopeService.GetScopeAsync(User);
        return scope is not null && scope.Includes(jobPosting);
    }

    private async Task<DashboardFilterOptionsViewModel> BuildFilterOptionsAsync(
        RecruitmentScope scope,
        CancellationToken cancellationToken)
    {
        var scopedJobPostings = scope.ApplyToJobPostings(dbContext.JobPostings);

        var departmentOptions = await scopedJobPostings
            .Select(
                jobPosting => new
                {
                    jobPosting.Position.DepartmentId,
                    jobPosting.Position.Department.Name
                })
            .Distinct()
            .OrderBy(department => department.Name)
            .Select(department => new SelectOptionViewModel(department.DepartmentId, department.Name))
            .ToListAsync(cancellationToken);

        var positionOptions = await scopedJobPostings
            .Select(jobPosting => new { jobPosting.PositionId, jobPosting.Position.Name })
            .Distinct()
            .OrderBy(position => position.Name)
            .Select(position => new SelectOptionViewModel(position.PositionId, position.Name))
            .ToListAsync(cancellationToken);

        var jobPostingOptions = await scopedJobPostings
            .OrderBy(jobPosting => jobPosting.Title)
            .Select(jobPosting => new SelectOptionViewModel(jobPosting.Id, jobPosting.Title))
            .ToListAsync(cancellationToken);

        return new DashboardFilterOptionsViewModel(
            ApplicationStatuses.All.ToList(), departmentOptions, positionOptions, jobPostingOptions);
    }

    private async Task<ApplicationPoolDetailViewModel> BuildDetailViewModelAsync(
        JobApplication application,
        CancellationToken cancellationToken)
    {
        var skills = await dbContext.CandidateProfileSkills
            .Where(link => link.CandidateProfileId == application.CandidateProfileId)
            .OrderBy(link => link.Skill.Name)
            .Select(link => link.Skill.Name)
            .ToListAsync(cancellationToken);

        var languages = await dbContext.CandidateProfileLanguages
            .Where(link => link.CandidateProfileId == application.CandidateProfileId)
            .OrderBy(link => link.Language.Name)
            .Select(link => link.Language.Name)
            .ToListAsync(cancellationToken);

        var educations = await dbContext.CandidateEducations
            .Where(education => education.CandidateProfileId == application.CandidateProfileId)
            .OrderByDescending(education => education.StartDate)
            .Select(
                education => new CandidateEducationSummaryViewModel(
                    education.Education.Name,
                    education.SchoolName,
                    education.FieldOfStudy,
                    education.StartDate,
                    education.EndDate))
            .ToListAsync(cancellationToken);

        var experiences = await dbContext.CandidateExperiences
            .Where(experience => experience.CandidateProfileId == application.CandidateProfileId)
            .OrderByDescending(experience => experience.StartDate)
            .Select(
                experience => new CandidateExperienceSummaryViewModel(
                    experience.CompanyName,
                    experience.JobTitle,
                    experience.StartDate,
                    experience.EndDate))
            .ToListAsync(cancellationToken);

        var documents = await dbContext.CandidateDocuments
            .Where(document => document.CandidateProfileId == application.CandidateProfileId)
            .OrderByDescending(document => document.UploadedAtUtc)
            .Select(
                document => new
                {
                    document.Id,
                    document.DocumentType,
                    document.OriginalFileName,
                    document.FileSizeBytes,
                    document.UploadedAtUtc
                })
            .ToListAsync(cancellationToken);

        var documentViewModels = documents
            .Select(
                document => new ApplicationDocumentViewModel(
                    document.Id,
                    CandidateDocumentTypes.GetDisplayLabel(document.DocumentType),
                    document.OriginalFileName,
                    document.FileSizeBytes,
                    document.UploadedAtUtc))
            .ToList();

        var notes = await dbContext.ApplicationNotes
            .Where(note => note.JobApplicationId == application.Id)
            .OrderByDescending(note => note.CreatedAtUtc)
            .Select(note => new { note.AuthorUserId, note.Body, note.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        var timelineLogs = await dbContext.ActivityLogs
            .Where(
                log =>
                    log.TargetEntityType == ActivityEntityTypes.Application &&
                    log.TargetEntityId == application.Id.ToString())
            .OrderByDescending(log => log.OccurredAtUtc)
            .Select(log => new { log.OccurredAtUtc, log.ActorUserId, log.ActionCode, log.Summary })
            .ToListAsync(cancellationToken);

        var actorIds = notes
            .Select(note => note.AuthorUserId)
            .Concat(
                timelineLogs
                    .Where(log => log.ActorUserId != null)
                    .Select(log => log.ActorUserId!))
            .Distinct()
            .ToList();

        var actorNames = await dbContext.Users
            .Where(user => actorIds.Contains(user.Id))
            .Select(user => new { user.Id, user.UserName })
            .ToDictionaryAsync(
                user => user.Id,
                user => user.UserName ?? string.Empty,
                cancellationToken);

        var noteViewModels = notes
            .Select(
                note => new ApplicationNoteViewModel(
                    actorNames.TryGetValue(note.AuthorUserId, out var authorName) ? authorName : "-",
                    note.Body,
                    note.CreatedAtUtc))
            .ToList();

        var timelineViewModels = timelineLogs
            .Select(
                log => new ApplicationTimelineEntryViewModel(
                    log.OccurredAtUtc.UtcDateTime,
                    log.ActorUserId is not null &&
                        actorNames.TryGetValue(log.ActorUserId, out var actorName)
                        ? actorName
                        : "Sistem",
                    DescribeActionCode(log.ActionCode)))
            .ToList();

        var interviewRows = await dbContext.Interviews
            .Where(interview => interview.JobApplicationId == application.Id)
            .OrderByDescending(interview => interview.StartAtUtc)
            .Select(
                interview => new
                {
                    interview.Id,
                    interview.InterviewType,
                    interview.StartAtUtc,
                    interview.EndAtUtc,
                    interview.OnlineMeetingLink,
                    interview.Location,
                    interview.Status
                })
            .ToListAsync(cancellationToken);

        var interviewIds = interviewRows.Select(row => row.Id).ToList();

        var participantRows = await dbContext.InterviewParticipants
            .Where(participant => interviewIds.Contains(participant.InterviewId))
            .Select(
                participant => new
                {
                    participant.InterviewId,
                    ParticipantName = participant.ParticipantUser.UserName
                })
            .ToListAsync(cancellationToken);

        var participantLookup = participantRows
            .GroupBy(row => row.InterviewId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(row => row.ParticipantName ?? string.Empty)
                    .ToList());

        var interviews = interviewRows
            .Select(
                row => new InterviewSummaryViewModel(
                    row.Id,
                    InterviewTypes.GetDisplayLabel(row.InterviewType),
                    row.StartAtUtc,
                    row.EndAtUtc,
                    row.OnlineMeetingLink,
                    row.Location,
                    InterviewStatuses.GetDisplayLabel(row.Status),
                    participantLookup.TryGetValue(row.Id, out var participantNames)
                        ? participantNames
                        : []))
            .ToList();

        var canScheduleInterview =
            User.IsInRole(SystemRoles.Admin) || User.IsInRole(SystemRoles.RecruitmentSpecialist);

        var participantOptions = canScheduleInterview
            ? await BuildParticipantOptionsAsync(cancellationToken)
            : [];

        var canManageStatus =
            User.IsInRole(SystemRoles.Admin) ||
            User.IsInRole(SystemRoles.RecruitmentSpecialist) ||
            User.IsInRole(SystemRoles.HiringManager);

        var canMoveToScreening =
            canManageStatus && application.Status == ApplicationStatuses.New;
        var canMoveToInterview =
            canManageStatus && ApplicationStatuses.IsValidTransition(application.Status, ApplicationStatuses.Interview);
        var canReject =
            canManageStatus && ApplicationStatuses.IsValidTransition(application.Status, ApplicationStatuses.Rejected);
        var canReevaluate =
            canManageStatus && application.Status == ApplicationStatuses.Rejected;
        var canArchive =
            canManageStatus &&
            !application.IsArchived &&
            (application.Status == ApplicationStatuses.Rejected || application.Status == ApplicationStatuses.Withdrawn);

        var existingOffer = await dbContext.Offers
            .Where(offer => offer.JobApplicationId == application.Id)
            .Select(offer => new { offer.Id, offer.Status })
            .FirstOrDefaultAsync(cancellationToken);

        var canCreateOffer =
            canScheduleInterview &&
            application.Status == ApplicationStatuses.Interview &&
            existingOffer is null;
        var offerId = existingOffer?.Id;
        var offerStatusLabel = existingOffer is not null
            ? OfferStatuses.GetDisplayLabel(existingOffer.Status)
            : null;

        return new ApplicationPoolDetailViewModel(
            application.Id,
            ApplicationStatuses.GetDisplayLabel(application.Status),
            application.AppliedAtUtc,
            application.WithdrawnAtUtc,
            $"{application.CandidateProfile.FirstName} {application.CandidateProfile.LastName}",
            application.CandidateProfile.ProfessionalSummary,
            application.CandidateProfile.TargetPosition?.Name,
            skills,
            languages,
            educations,
            experiences,
            documentViewModels,
            application.JobPosting.Title,
            application.JobPosting.Position.Name,
            application.JobPosting.Position.Department.Name,
            application.JobPosting.Status,
            application.JobPosting.ApplicationDeadline,
            noteViewModels,
            timelineViewModels,
            interviews,
            canScheduleInterview,
            participantOptions,
            canMoveToScreening,
            canMoveToInterview,
            canReject,
            canReevaluate,
            canArchive,
            canCreateOffer,
            offerId,
            offerStatusLabel);
    }

    private async Task<IReadOnlyList<ParticipantOptionViewModel>> BuildParticipantOptionsAsync(
        CancellationToken cancellationToken)
    {
        var roleIds = await dbContext.Roles
            .Where(role => role.Name != null && InternalRoleNames.Contains(role.Name))
            .Select(role => role.Id)
            .ToListAsync(cancellationToken);

        var internalUserIds = dbContext.UserRoles
            .Where(userRole => roleIds.Contains(userRole.RoleId))
            .Select(userRole => userRole.UserId);

        return await dbContext.Users
            .Where(user => user.IsActive && internalUserIds.Contains(user.Id))
            .OrderBy(user => user.UserName)
            .Select(user => new ParticipantOptionViewModel(user.Id, user.UserName ?? user.Id))
            .ToListAsync(cancellationToken);
    }

    private static string DescribeActionCode(string actionCode)
    {
        return actionCode switch
        {
            ActivityActionCodes.EntityCreated => "Başvuru oluşturuldu",
            ActivityActionCodes.EntityStatusChanged => "Durum değişti",
            ActivityActionCodes.EntityDownloaded => "Belge indirildi",
            _ => actionCode
        };
    }
}
