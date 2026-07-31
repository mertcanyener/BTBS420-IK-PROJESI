namespace BTBS420.RecruitmentSystem.Web.ViewModels.JobApplications;

public sealed record JobApplicationListItemViewModel(
    int JobPostingId,
    string JobPostingTitle,
    string PositionName,
    string JobPostingStatus,
    DateTime AppliedAtUtc);
