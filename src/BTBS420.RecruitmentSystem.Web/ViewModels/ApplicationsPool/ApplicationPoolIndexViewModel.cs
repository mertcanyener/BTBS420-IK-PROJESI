namespace BTBS420.RecruitmentSystem.Web.ViewModels.ApplicationsPool;

public sealed class ApplicationPoolIndexViewModel(
    IReadOnlyList<ApplicationPoolListItemViewModel> applications,
    IReadOnlyList<string> statusOptions,
    string? selectedStatus)
{
    public IReadOnlyList<ApplicationPoolListItemViewModel> Applications { get; } =
        applications ?? throw new ArgumentNullException(nameof(applications));

    public IReadOnlyList<string> StatusOptions { get; } =
        statusOptions ?? throw new ArgumentNullException(nameof(statusOptions));

    public string? SelectedStatus { get; } = selectedStatus;
}
