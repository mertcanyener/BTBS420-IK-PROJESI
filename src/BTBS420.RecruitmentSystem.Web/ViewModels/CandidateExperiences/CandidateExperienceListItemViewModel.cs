namespace BTBS420.RecruitmentSystem.Web.ViewModels.CandidateExperiences;

public sealed record CandidateExperienceListItemViewModel(
    int Id,
    string CompanyName,
    string JobTitle,
    DateOnly StartDate,
    DateOnly? EndDate);
