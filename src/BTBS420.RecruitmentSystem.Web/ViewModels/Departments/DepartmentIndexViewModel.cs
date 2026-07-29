namespace BTBS420.RecruitmentSystem.Web.ViewModels.Departments;

public sealed class DepartmentIndexViewModel(
    IReadOnlyList<DepartmentListItemViewModel> departments)
{
    public IReadOnlyList<DepartmentListItemViewModel> Departments { get; } =
        departments ?? throw new ArgumentNullException(nameof(departments));
}
