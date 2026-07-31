namespace BTBS420.RecruitmentSystem.Web.ViewModels.ApplicationsPool;

public sealed record ApplicationNoteViewModel(
    string AuthorName,
    string Body,
    DateTime CreatedAtUtc);
