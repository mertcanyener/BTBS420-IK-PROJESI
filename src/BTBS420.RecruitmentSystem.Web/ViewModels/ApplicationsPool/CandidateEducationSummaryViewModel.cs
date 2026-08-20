namespace BTBS420.RecruitmentSystem.Web.ViewModels.ApplicationsPool;

public sealed record CandidateEducationSummaryViewModel(
    string EducationLevelName,
    string SchoolName,
    string? FieldOfStudy,
    DateOnly StartDate,
    DateOnly? EndDate);
