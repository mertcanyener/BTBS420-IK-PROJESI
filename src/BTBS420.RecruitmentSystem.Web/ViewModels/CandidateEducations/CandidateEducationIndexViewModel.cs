namespace BTBS420.RecruitmentSystem.Web.ViewModels.CandidateEducations;

public sealed class CandidateEducationIndexViewModel(
    IReadOnlyList<CandidateEducationListItemViewModel> educations)
{
    public IReadOnlyList<CandidateEducationListItemViewModel> Educations { get; } =
        educations ?? throw new ArgumentNullException(nameof(educations));
}
