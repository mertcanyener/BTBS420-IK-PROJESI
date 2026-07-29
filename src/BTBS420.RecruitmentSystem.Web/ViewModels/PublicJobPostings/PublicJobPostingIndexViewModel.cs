using BTBS420.RecruitmentSystem.Web.ViewModels.Positions;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.PublicJobPostings;

public sealed class PublicJobPostingIndexViewModel(
    IReadOnlyList<PublicJobPostingListItemViewModel> jobPostings,
    IReadOnlyList<SelectOptionViewModel> positionOptions,
    int? positionId,
    int page,
    int pageSize,
    int totalCount)
{
    public IReadOnlyList<PublicJobPostingListItemViewModel> JobPostings { get; } =
        jobPostings ?? throw new ArgumentNullException(nameof(jobPostings));

    public IReadOnlyList<SelectOptionViewModel> PositionOptions { get; } =
        positionOptions ?? throw new ArgumentNullException(nameof(positionOptions));

    public int? PositionId { get; } = positionId;

    public int Page { get; } = page;

    public int PageSize { get; } = pageSize;

    public int TotalCount { get; } = totalCount;

    public int TotalPages { get; } = (int)Math.Ceiling(totalCount / (double)pageSize);
}
