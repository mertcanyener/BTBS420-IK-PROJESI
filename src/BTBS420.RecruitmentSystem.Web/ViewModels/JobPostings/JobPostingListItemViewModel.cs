namespace BTBS420.RecruitmentSystem.Web.ViewModels.JobPostings;

public sealed record JobPostingListItemViewModel(
    int Id,
    string Title,
    string PositionName,
    string DepartmentName,
    string ResponsibleUserName,
    DateOnly ApplicationDeadline,
    string Status);
