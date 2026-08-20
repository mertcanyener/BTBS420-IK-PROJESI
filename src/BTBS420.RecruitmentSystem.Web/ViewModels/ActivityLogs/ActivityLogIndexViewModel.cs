namespace BTBS420.RecruitmentSystem.Web.ViewModels.ActivityLogs;

public sealed class ActivityLogIndexViewModel(
    IReadOnlyList<ActivityLogListItemViewModel> entries,
    ActivityLogFilterViewModel filter,
    ActivityLogFilterOptionsViewModel filterOptions,
    int totalCount)
{
    public IReadOnlyList<ActivityLogListItemViewModel> Entries { get; } =
        entries ?? throw new ArgumentNullException(nameof(entries));

    public ActivityLogFilterViewModel Filter { get; } =
        filter ?? throw new ArgumentNullException(nameof(filter));

    public ActivityLogFilterOptionsViewModel FilterOptions { get; } =
        filterOptions ?? throw new ArgumentNullException(nameof(filterOptions));

    public int TotalCount { get; } = totalCount;

    public int TotalPages { get; } = (int)Math.Ceiling(totalCount / (double)Math.Max(filter.PageSize, 1));
}
