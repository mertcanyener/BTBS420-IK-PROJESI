using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.Storage;
using BTBS420.RecruitmentSystem.Web.ViewModels.CandidateDocuments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize(Roles = SystemRoles.Candidate)]
public sealed class CandidateDocumentsController(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IActivityLogService activityLogService,
    ICandidateDocumentStorageService storageService,
    IOptions<CandidateDocumentStorageOptions> storageOptions) : Controller
{
    private const string ProfileRequiredMessage =
        "Belge eklemeden önce profilinizi oluşturmalısınız.";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var profile = await GetCurrentProfileAsync(cancellationToken);
        if (profile is null)
        {
            TempData["StatusMessage"] = ProfileRequiredMessage;
            return RedirectToAction("Index", "CandidateProfile");
        }

        var documents = await dbContext.CandidateDocuments
            .Where(document => document.CandidateProfileId == profile.Id)
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

        return View(new CandidateDocumentIndexViewModel(listItems));
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var profile = await GetCurrentProfileAsync(cancellationToken);
        if (profile is null)
        {
            TempData["StatusMessage"] = ProfileRequiredMessage;
            return RedirectToAction("Index", "CandidateProfile");
        }

        return View(
            new CandidateDocumentFormViewModel { DocumentTypeOptions = BuildDocumentTypeOptions() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = 20 * 1024 * 1024)]
    public async Task<IActionResult> Create(
        CandidateDocumentFormViewModel model,
        CancellationToken cancellationToken)
    {
        var profile = await GetCurrentProfileAsync(cancellationToken);
        if (profile is null)
        {
            TempData["StatusMessage"] = ProfileRequiredMessage;
            return RedirectToAction("Index", "CandidateProfile");
        }

        if (!ModelState.IsValid)
        {
            model.DocumentTypeOptions = BuildDocumentTypeOptions();
            return View(model);
        }

        var validation = await CandidateDocumentValidation.ValidateAsync(
            model.File!,
            storageOptions.Value.MaxFileSizeBytes,
            cancellationToken);

        if (!validation.IsValid)
        {
            ModelState.AddModelError(nameof(model.File), validation.ErrorMessage!);
            model.DocumentTypeOptions = BuildDocumentTypeOptions();
            return View(model);
        }

        var storedFileName = $"{Guid.NewGuid():N}{validation.Extension}";

        CandidateDocument document;
        try
        {
            document = new CandidateDocument(
                profile.Id,
                model.DocumentType,
                model.File!.FileName,
                storedFileName,
                validation.ContentType!,
                model.File.Length,
                DateTime.UtcNow);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            model.DocumentTypeOptions = BuildDocumentTypeOptions();
            return View(model);
        }

        await using (var stream = model.File!.OpenReadStream())
        {
            await storageService.SaveAsync(profile.Id, storedFileName, stream, cancellationToken);
        }

        dbContext.CandidateDocuments.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityCreated,
                "Aday belgesi yüklendi.",
                ActivityEntityTypes.CandidateDocument,
                document.Id.ToString(),
                CandidateId: profile.ApplicationUserId));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var profile = await GetCurrentProfileAsync(cancellationToken);
        if (profile is null)
        {
            TempData["StatusMessage"] = ProfileRequiredMessage;
            return RedirectToAction("Index", "CandidateProfile");
        }

        var document = await dbContext.CandidateDocuments
            .FirstOrDefaultAsync(
                candidateDocument =>
                    candidateDocument.Id == id &&
                    candidateDocument.CandidateProfileId == profile.Id,
                cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        dbContext.CandidateDocuments.Remove(document);

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityDeleted,
                "Aday belgesi silindi.",
                ActivityEntityTypes.CandidateDocument,
                document.Id.ToString(),
                CandidateId: profile.ApplicationUserId));
        await dbContext.SaveChangesAsync(cancellationToken);

        storageService.Delete(profile.Id, document.StoredFileName);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var profile = await GetCurrentProfileAsync(cancellationToken);
        if (profile is null)
        {
            TempData["StatusMessage"] = ProfileRequiredMessage;
            return RedirectToAction("Index", "CandidateProfile");
        }

        var document = await dbContext.CandidateDocuments
            .FirstOrDefaultAsync(
                candidateDocument =>
                    candidateDocument.Id == id &&
                    candidateDocument.CandidateProfileId == profile.Id,
                cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityDownloaded,
                "Aday kendi belgesini indirdi.",
                ActivityEntityTypes.CandidateDocument,
                document.Id.ToString(),
                CandidateId: profile.ApplicationUserId));
        await dbContext.SaveChangesAsync(cancellationToken);

        var stream = storageService.OpenRead(profile.Id, document.StoredFileName);
        return File(stream, document.ContentType, document.OriginalFileName);
    }

    private async Task<CandidateProfile?> GetCurrentProfileAsync(CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null)
        {
            return null;
        }

        return await dbContext.CandidateProfiles
            .FirstOrDefaultAsync(profile => profile.ApplicationUserId == userId, cancellationToken);
    }

    private static IReadOnlyList<CandidateDocumentTypeOptionViewModel> BuildDocumentTypeOptions()
    {
        return CandidateDocumentTypes.All
            .Select(
                type => new CandidateDocumentTypeOptionViewModel(
                    type,
                    CandidateDocumentTypes.GetDisplayLabel(type)))
            .OrderBy(option => option.Label, StringComparer.Ordinal)
            .ToList();
    }
}
