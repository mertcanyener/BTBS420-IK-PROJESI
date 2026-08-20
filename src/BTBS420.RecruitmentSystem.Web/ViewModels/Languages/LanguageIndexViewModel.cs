namespace BTBS420.RecruitmentSystem.Web.ViewModels.Languages;

public sealed class LanguageIndexViewModel(
    IReadOnlyList<LanguageListItemViewModel> languages)
{
    public IReadOnlyList<LanguageListItemViewModel> Languages { get; } =
        languages ?? throw new ArgumentNullException(nameof(languages));
}
