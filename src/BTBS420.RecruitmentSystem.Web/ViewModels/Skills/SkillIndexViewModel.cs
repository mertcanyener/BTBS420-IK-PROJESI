namespace BTBS420.RecruitmentSystem.Web.ViewModels.Skills;

public sealed class SkillIndexViewModel(IReadOnlyList<SkillListItemViewModel> skills)
{
    public IReadOnlyList<SkillListItemViewModel> Skills { get; } =
        skills ?? throw new ArgumentNullException(nameof(skills));
}
