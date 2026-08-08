namespace BTBS420.RecruitmentSystem.Web.Ai.Evaluation;

public sealed record CandidateEducationSnapshot(
    string SchoolName,
    string? FieldOfStudy,
    string EducationName,
    DateOnly StartDate,
    DateOnly? EndDate);
