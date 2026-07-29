namespace BTBS420.RecruitmentSystem.Web.ViewModels.Users;

public sealed record UserDetailsViewModel(
    string Id,
    string UserName,
    string? Email,
    IReadOnlyList<string> Roles,
    string? DepartmentName,
    bool IsActive);
