namespace BTBS420.RecruitmentSystem.Web.ViewModels.JobPostings;

public sealed record JobPostingDetailViewModel(
    int Id,
    string Title,
    string Description,
    string PositionName,
    string DepartmentName,
    string ResponsibleUserName,
    DateOnly ApplicationDeadline,
    string Status);
