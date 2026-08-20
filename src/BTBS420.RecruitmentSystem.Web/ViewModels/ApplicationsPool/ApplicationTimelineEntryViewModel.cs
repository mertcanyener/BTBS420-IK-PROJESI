namespace BTBS420.RecruitmentSystem.Web.ViewModels.ApplicationsPool;

public sealed record ApplicationTimelineEntryViewModel(
    DateTime OccurredAtUtc,
    string ActorName,
    string Description);
