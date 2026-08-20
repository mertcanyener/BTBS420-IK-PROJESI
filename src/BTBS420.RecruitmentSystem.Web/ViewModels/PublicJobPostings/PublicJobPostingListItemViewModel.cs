namespace BTBS420.RecruitmentSystem.Web.ViewModels.PublicJobPostings;

public sealed record PublicJobPostingListItemViewModel(
    int Id,
    string Title,
    string PositionName,
    string DepartmentName,
    DateOnly ApplicationDeadline);
