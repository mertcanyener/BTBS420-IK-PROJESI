namespace BTBS420.RecruitmentSystem.Web.ViewModels.ExperienceRanges;

public sealed class ExperienceRangeIndexViewModel(
    IReadOnlyList<ExperienceRangeListItemViewModel> experienceRanges)
{
    public IReadOnlyList<ExperienceRangeListItemViewModel> ExperienceRanges { get; } =
        experienceRanges ?? throw new ArgumentNullException(nameof(experienceRanges));
}
