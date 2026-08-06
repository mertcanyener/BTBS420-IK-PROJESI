using BTBS420.RecruitmentSystem.Web.ViewModels.Dashboard;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.ApplicationsPool;

public sealed class ApplicationPoolIndexViewModel(
    IReadOnlyList<ApplicationPoolListItemViewModel> applications,
    DashboardFilterViewModel filter,
    DashboardFilterOptionsViewModel filterOptions,
    int totalCount)
{
    public IReadOnlyList<ApplicationPoolListItemViewModel> Applications { get; } =
        applications ?? throw new ArgumentNullException(nameof(applications));

    public DashboardFilterViewModel Filter { get; } =
        filter ?? throw new ArgumentNullException(nameof(filter));

    public DashboardFilterOptionsViewModel FilterOptions { get; } =
        filterOptions ?? throw new ArgumentNullException(nameof(filterOptions));

    public int TotalCount { get; } = totalCount;

    public int TotalPages { get; } = (int)Math.Ceiling(totalCount / (double)Math.Max(filter.PageSize, 1));
}
