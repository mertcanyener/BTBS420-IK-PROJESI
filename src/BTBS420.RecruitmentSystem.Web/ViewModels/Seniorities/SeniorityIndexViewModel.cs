namespace BTBS420.RecruitmentSystem.Web.ViewModels.Seniorities;

public sealed class SeniorityIndexViewModel(
    IReadOnlyList<SeniorityListItemViewModel> seniorities)
{
    public IReadOnlyList<SeniorityListItemViewModel> Seniorities { get; } =
        seniorities ?? throw new ArgumentNullException(nameof(seniorities));
}
