namespace BTBS420.RecruitmentSystem.Web.ViewModels.ApplicationsPool;

public sealed record CandidateExperienceSummaryViewModel(
    string CompanyName,
    string JobTitle,
    DateOnly StartDate,
    DateOnly? EndDate);
