namespace BTBS420.RecruitmentSystem.Web.ViewModels.JobPostings;

public sealed class JobPostingIndexViewModel(
    IReadOnlyList<JobPostingListItemViewModel> jobPostings)
{
    public IReadOnlyList<JobPostingListItemViewModel> JobPostings { get; } =
        jobPostings ?? throw new ArgumentNullException(nameof(jobPostings));
}
