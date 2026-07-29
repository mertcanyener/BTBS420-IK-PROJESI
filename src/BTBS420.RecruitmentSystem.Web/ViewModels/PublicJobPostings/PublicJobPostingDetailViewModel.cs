namespace BTBS420.RecruitmentSystem.Web.ViewModels.PublicJobPostings;

public sealed record PublicJobPostingDetailViewModel(
    int Id,
    string Title,
    string Description,
    string PositionName,
    string DepartmentName,
    DateOnly ApplicationDeadline);
