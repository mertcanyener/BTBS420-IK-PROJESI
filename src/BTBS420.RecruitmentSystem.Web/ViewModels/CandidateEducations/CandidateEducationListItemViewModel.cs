namespace BTBS420.RecruitmentSystem.Web.ViewModels.CandidateEducations;

public sealed record CandidateEducationListItemViewModel(
    int Id,
    string EducationName,
    string SchoolName,
    string? FieldOfStudy,
    DateOnly StartDate,
    DateOnly? EndDate);
