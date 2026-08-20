namespace BTBS420.RecruitmentSystem.Web.ViewModels.ApplicationsPool;

public sealed record ApplicationDocumentViewModel(
    int Id,
    string DocumentTypeLabel,
    string OriginalFileName,
    long FileSizeBytes,
    DateTime UploadedAtUtc);
