namespace BTBS420.RecruitmentSystem.Web.ViewModels.ExperienceRanges;

public sealed record ExperienceRangeListItemViewModel(
    int Id,
    string Name,
    int MinYears,
    int MaxYears,
    bool IsActive);
