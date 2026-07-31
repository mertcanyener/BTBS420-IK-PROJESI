using System.Data;
using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.Storage;
using BTBS420.RecruitmentSystem.Web.ViewModels.ApplicationsPool;
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
    ICandidateDocumentStorageService storageService,
    TimeProvider timeProvider) : Controller
{
    private const string NoteRequiredMessage = "Not metni boş olamaz.";

    private const string NoteAddedMessage = "Not eklendi.";

    private const string InterviewScheduledMessage = "Mülakat planlandı.";

    private const string ParticipantRequiredMessage = "En az bir katılımcı seçmelisiniz.";

    private const string ParticipantsAlreadyAssignedMessage =
        "Seçilen katılımcılar zaten bu mülakata atanmış.";

    private const string ParticipantsAssignedMessage = "Katılımcılar atandı.";

    private const string OperationFailedMessage = "İşlem tamamlanamadı, lütfen tekrar deneyin.";

    private static readonly string[] InternalRoleNames =
    [
        SystemRoles.Admin,
        SystemRoles.RecruitmentSpecialist,
        SystemRoles.HiringManager
    ];

    [HttpGet]
    public async Task<IActionResult> Index(string? status, CancellationToken cancellationToken)
    {
        var query = dbContext.JobApplications.AsQueryable();

        if (!User.IsInRole(SystemRoles.Admin))
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser is null)
            {
                return Forbid();
            }

            if (User.IsInRole(SystemRoles.RecruitmentSpecialist))
            {
                query = query.Where(
                    application => application.JobPosting.ResponsibleUserId == currentUser.Id);
            }
            else if (User.IsInRole(SystemRoles.HiringManager))
            {
                query = currentUser.DepartmentId is null
                    ? query.Where(application => false)
                    : query.Where(
                        application =>
                            application.JobPosting.Position.DepartmentId ==
                            currentUser.DepartmentId.Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(status) && ApplicationStatuses.IsDefined(status))
        {
            query = query.Where(application => application.Status == status);
        }

        var applications = await query
            .OrderByDescending(application => application.AppliedAtUtc)
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

        return View(
            new ApplicationPoolIndexViewModel(listItems, ApplicationStatuses.All.ToList(), status));
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

    private async Task<bool> IsAuthorizedForApplicationAsync(JobPosting jobPosting)
    {
        if (User.IsInRole(SystemRoles.Admin))
        {
            return true;
        }

        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return false;
        }

        if (User.IsInRole(SystemRoles.RecruitmentSpecialist))
        {
            return jobPosting.ResponsibleUserId == currentUser.Id;
        }

        if (User.IsInRole(SystemRoles.HiringManager))
        {
            return currentUser.DepartmentId is not null &&
                jobPosting.Position.DepartmentId == currentUser.DepartmentId.Value;
        }

        return false;
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
            participantOptions);
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
