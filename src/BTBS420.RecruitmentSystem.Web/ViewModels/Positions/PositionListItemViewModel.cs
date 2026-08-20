namespace BTBS420.RecruitmentSystem.Web.ViewModels.Positions;

public sealed record PositionListItemViewModel(
    int Id,
    string Name,
    string DepartmentName,
    string? JobFamilyName,
    string? SeniorityName,
    bool IsActive);
