using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.Storage;
using BTBS420.RecruitmentSystem.Web.ViewModels.CandidateDocuments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.RecruitmentStaffOnly)]
public sealed class StaffCandidateDocumentsController(
    ApplicationDbContext dbContext,
    IActivityLogService activityLogService,
    ICandidateDocumentStorageService storageService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int candidateProfileId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.CandidateProfiles
            .FirstOrDefaultAsync(p => p.Id == candidateProfileId, cancellationToken);

        if (profile is null)
        {
            return NotFound();
        }

        var documents = await dbContext.CandidateDocuments
            .Where(document => document.CandidateProfileId == candidateProfileId)
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

        var listItems = documents
            .Select(
                document => new CandidateDocumentListItemViewModel(
                    document.Id,
                    CandidateDocumentTypes.GetDisplayLabel(document.DocumentType),
                    document.OriginalFileName,
                    document.FileSizeBytes,
                    document.UploadedAtUtc))
            .ToList();

        return View(
            new StaffCandidateDocumentIndexViewModel(
                profile.Id,
                $"{profile.FirstName} {profile.LastName}",
                listItems));
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var document = await dbContext.CandidateDocuments
            .Include(candidateDocument => candidateDocument.CandidateProfile)
            .FirstOrDefaultAsync(candidateDocument => candidateDocument.Id == id, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityDownloaded,
                "Yetkili personel aday belgesini indirdi.",
                ActivityEntityTypes.CandidateDocument,
                document.Id.ToString(),
                CandidateId: document.CandidateProfile.ApplicationUserId));
        await dbContext.SaveChangesAsync(cancellationToken);

        var stream = storageService.OpenRead(document.CandidateProfileId, document.StoredFileName);
        return File(stream, document.ContentType, document.OriginalFileName);
    }
}
