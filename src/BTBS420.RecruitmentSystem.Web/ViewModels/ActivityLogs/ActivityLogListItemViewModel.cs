namespace BTBS420.RecruitmentSystem.Web.ViewModels.ActivityLogs;

public sealed record ActivityLogListItemViewModel(
    long Id,
    DateTimeOffset OccurredAtUtc,
    string? ActorUserId,
    string ActorName,
    string ActionLabel,
    string? TargetEntityType,
    string? TargetEntityId,
    string? JobPostingTitle,
    string? CandidateName,
    string Summary);
