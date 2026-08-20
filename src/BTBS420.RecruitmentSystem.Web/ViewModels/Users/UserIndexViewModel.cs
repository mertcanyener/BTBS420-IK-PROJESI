namespace BTBS420.RecruitmentSystem.Web.ViewModels.Users;

public sealed class UserIndexViewModel(
    IReadOnlyList<UserListItemViewModel> users,
    IReadOnlyList<string> roleOptions,
    IReadOnlyList<DepartmentOptionViewModel> departmentOptions,
    string? search,
    string? role,
    int? departmentId,
    bool? isActive)
{
    public IReadOnlyList<UserListItemViewModel> Users { get; } =
        users ?? throw new ArgumentNullException(nameof(users));

    public IReadOnlyList<string> RoleOptions { get; } =
        roleOptions ?? throw new ArgumentNullException(nameof(roleOptions));

    public IReadOnlyList<DepartmentOptionViewModel> DepartmentOptions { get; } =
        departmentOptions ?? throw new ArgumentNullException(nameof(departmentOptions));

    public string? Search { get; } = search;

    public string? Role { get; } = role;

    public int? DepartmentId { get; } = departmentId;

    public bool? IsActive { get; } = isActive;
}
