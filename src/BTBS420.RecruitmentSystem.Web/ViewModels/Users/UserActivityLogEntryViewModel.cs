namespace BTBS420.RecruitmentSystem.Web.ViewModels.Users;

public sealed record UserActivityLogEntryViewModel(
    DateTimeOffset OccurredAtUtc,
    string ActorName,
    string ActionLabel,
    string Summary);
