namespace BTBS420.RecruitmentSystem.Web.ViewModels.ActivityLogs;

public sealed record ActivityLogFilterViewModel(
    DateOnly? DateFrom,
    DateOnly? DateTo,
    string? UserId,
    int? JobPostingId,
    string? CandidateId,
    string? ActionCode,
    int Page,
    int PageSize);
