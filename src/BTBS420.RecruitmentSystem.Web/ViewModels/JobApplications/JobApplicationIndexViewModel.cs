namespace BTBS420.RecruitmentSystem.Web.ViewModels.JobApplications;

public sealed class JobApplicationIndexViewModel(
    IReadOnlyList<JobApplicationListItemViewModel> applications)
{
    public IReadOnlyList<JobApplicationListItemViewModel> Applications { get; } =
        applications ?? throw new ArgumentNullException(nameof(applications));
}
