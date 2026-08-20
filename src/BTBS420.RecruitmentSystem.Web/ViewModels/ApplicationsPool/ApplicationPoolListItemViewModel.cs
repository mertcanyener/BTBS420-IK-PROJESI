namespace BTBS420.RecruitmentSystem.Web.ViewModels.ApplicationsPool;

public sealed record ApplicationPoolListItemViewModel(
    int Id,
    string CandidateFullName,
    string JobPostingTitle,
    string PositionName,
    string DepartmentName,
    string ApplicationStatusLabel,
    DateTime AppliedAtUtc);
