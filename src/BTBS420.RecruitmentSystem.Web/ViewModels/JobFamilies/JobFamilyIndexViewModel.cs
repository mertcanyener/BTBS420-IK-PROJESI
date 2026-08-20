namespace BTBS420.RecruitmentSystem.Web.ViewModels.JobFamilies;

public sealed class JobFamilyIndexViewModel(
    IReadOnlyList<JobFamilyListItemViewModel> jobFamilies)
{
    public IReadOnlyList<JobFamilyListItemViewModel> JobFamilies { get; } =
        jobFamilies ?? throw new ArgumentNullException(nameof(jobFamilies));
}
