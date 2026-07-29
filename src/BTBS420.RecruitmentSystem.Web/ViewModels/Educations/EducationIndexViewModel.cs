namespace BTBS420.RecruitmentSystem.Web.ViewModels.Educations;

public sealed class EducationIndexViewModel(
    IReadOnlyList<EducationListItemViewModel> educations)
{
    public IReadOnlyList<EducationListItemViewModel> Educations { get; } =
        educations ?? throw new ArgumentNullException(nameof(educations));
}
