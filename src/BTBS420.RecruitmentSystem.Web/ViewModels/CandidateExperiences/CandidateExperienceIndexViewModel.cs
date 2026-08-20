namespace BTBS420.RecruitmentSystem.Web.ViewModels.CandidateExperiences;

public sealed class CandidateExperienceIndexViewModel(
    IReadOnlyList<CandidateExperienceListItemViewModel> experiences)
{
    public IReadOnlyList<CandidateExperienceListItemViewModel> Experiences { get; } =
        experiences ?? throw new ArgumentNullException(nameof(experiences));
}
