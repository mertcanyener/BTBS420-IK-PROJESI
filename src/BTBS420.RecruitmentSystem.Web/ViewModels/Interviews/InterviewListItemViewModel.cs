namespace BTBS420.RecruitmentSystem.Web.ViewModels.Interviews;

public sealed record InterviewListItemViewModel(
    int Id,
    string CandidateFullName,
    string JobPostingTitle,
    string InterviewTypeLabel,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string StatusLabel);
