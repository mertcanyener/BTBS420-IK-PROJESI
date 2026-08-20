namespace BTBS420.RecruitmentSystem.Web.ViewModels.CandidateDocuments;

public sealed record CandidateDocumentListItemViewModel(
    int Id,
    string DocumentTypeLabel,
    string OriginalFileName,
    long FileSizeBytes,
    DateTime UploadedAtUtc);
