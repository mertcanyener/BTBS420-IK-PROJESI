namespace BTBS420.RecruitmentSystem.Web.ViewModels.JobPostings;

public sealed class JobPostingIndexViewModel(
    IReadOnlyList<JobPostingListItemViewModel> jobPostings,
    IReadOnlyList<string> statusOptions,
    string? status,
    DateOnly? deadlineFrom,
    DateOnly? deadlineTo)
{
    public IReadOnlyList<JobPostingListItemViewModel> JobPostings { get; } =
        jobPostings ?? throw new ArgumentNullException(nameof(jobPostings));

    public IReadOnlyList<string> StatusOptions { get; } =
        statusOptions ?? throw new ArgumentNullException(nameof(statusOptions));

    public string? Status { get; } = status;

    public DateOnly? DeadlineFrom { get; } = deadlineFrom;

    public DateOnly? DeadlineTo { get; } = deadlineTo;
}
