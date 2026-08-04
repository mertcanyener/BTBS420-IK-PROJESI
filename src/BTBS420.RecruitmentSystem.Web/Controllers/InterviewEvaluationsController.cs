using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize]
public sealed class InterviewEvaluationsController(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IActivityLogService activityLogService,
    TimeProvider timeProvider) : Controller
{
    private const string DuplicateEvaluationMessage = "Bu mülakat için zaten bir değerlendirmeniz var.";

    private const string ConcurrencyConflictMessage =
        "Değerlendirmeniz siz işlem yaparken başka bir işlemle güncellendi, lütfen tekrar deneyin.";

    private const string EvaluationSavedMessage = "Değerlendirmeniz kaydedildi.";

    private const string EvaluationDeletedMessage = "Değerlendirmeniz silindi.";

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        int interviewId,
        string? note,
        int competencyScore,
        int overallScore,
        string recommendation,
        CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null)
        {
            return Forbid();
        }

        var interview = await dbContext.Interviews
            .Include(candidateInterview => candidateInterview.JobApplication)
            .ThenInclude(candidateApplication => candidateApplication.CandidateProfile)
            .FirstOrDefaultAsync(candidateInterview => candidateInterview.Id == interviewId, cancellationToken);

        if (interview is null || !await IsParticipantAsync(interviewId, userId, cancellationToken))
        {
            return NotFound();
        }

        InterviewEvaluation evaluation;
        try
        {
            evaluation = new InterviewEvaluation(
                interviewId,
                userId,
                note,
                competencyScore,
                overallScore,
                recommendation,
                timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (ArgumentException exception)
        {
            TempData["StatusMessage"] = exception.Message;
            return RedirectToAction("Details", "Interviews", new { id = interviewId });
        }

        dbContext.InterviewEvaluations.Add(evaluation);

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityCreated,
                "Panel üyesi mülakat değerlendirmesi oluşturdu.",
                ActivityEntityTypes.Interview,
                interviewId.ToString(),
                JobPostingId: interview.JobApplication.JobPostingId.ToString(),
                CandidateId: interview.JobApplication.CandidateProfile.ApplicationUserId));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            TempData["StatusMessage"] = DuplicateEvaluationMessage;
            return RedirectToAction("Details", "Interviews", new { id = interviewId });
        }

        TempData["StatusMessage"] = EvaluationSavedMessage;
        return RedirectToAction("Details", "Interviews", new { id = interviewId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        string? note,
        int competencyScore,
        int overallScore,
        string recommendation,
        CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null)
        {
            return Forbid();
        }

        var evaluation = await dbContext.InterviewEvaluations
            .Include(interviewEvaluation => interviewEvaluation.Interview)
            .ThenInclude(interview => interview.JobApplication)
            .ThenInclude(jobApplication => jobApplication.CandidateProfile)
            .FirstOrDefaultAsync(
                interviewEvaluation =>
                    interviewEvaluation.Id == id && interviewEvaluation.EvaluatorUserId == userId,
                cancellationToken);

        if (evaluation is null)
        {
            return NotFound();
        }

        try
        {
            evaluation.Edit(note, competencyScore, overallScore, recommendation);
        }
        catch (ArgumentException exception)
        {
            TempData["StatusMessage"] = exception.Message;
            return RedirectToAction("Details", "Interviews", new { id = evaluation.InterviewId });
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityUpdated,
                "Panel üyesi mülakat değerlendirmesini güncelledi.",
                ActivityEntityTypes.Interview,
                evaluation.InterviewId.ToString(),
                JobPostingId: evaluation.Interview.JobApplication.JobPostingId.ToString(),
                CandidateId: evaluation.Interview.JobApplication.CandidateProfile.ApplicationUserId));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["StatusMessage"] = ConcurrencyConflictMessage;
            return RedirectToAction("Details", "Interviews", new { id = evaluation.InterviewId });
        }

        TempData["StatusMessage"] = EvaluationSavedMessage;
        return RedirectToAction("Details", "Interviews", new { id = evaluation.InterviewId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null)
        {
            return Forbid();
        }

        var evaluation = await dbContext.InterviewEvaluations
            .Include(interviewEvaluation => interviewEvaluation.Interview)
            .ThenInclude(interview => interview.JobApplication)
            .ThenInclude(jobApplication => jobApplication.CandidateProfile)
            .FirstOrDefaultAsync(
                interviewEvaluation =>
                    interviewEvaluation.Id == id && interviewEvaluation.EvaluatorUserId == userId,
                cancellationToken);

        if (evaluation is null)
        {
            return NotFound();
        }

        var interviewId = evaluation.InterviewId;

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityDeleted,
                "Panel üyesi mülakat değerlendirmesini sildi.",
                ActivityEntityTypes.Interview,
                interviewId.ToString(),
                JobPostingId: evaluation.Interview.JobApplication.JobPostingId.ToString(),
                CandidateId: evaluation.Interview.JobApplication.CandidateProfile.ApplicationUserId));

        dbContext.InterviewEvaluations.Remove(evaluation);
        await dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = EvaluationDeletedMessage;
        return RedirectToAction("Details", "Interviews", new { id = interviewId });
    }

    private async Task<bool> IsParticipantAsync(
        int interviewId,
        string userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.InterviewParticipants
            .AnyAsync(
                participant =>
                    participant.InterviewId == interviewId && participant.ParticipantUserId == userId,
                cancellationToken);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException &&
            sqlException.Number is 2601 or 2627;
    }
}
