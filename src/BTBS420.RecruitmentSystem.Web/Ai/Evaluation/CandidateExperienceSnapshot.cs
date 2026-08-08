namespace BTBS420.RecruitmentSystem.Web.Ai.Evaluation;

public sealed record CandidateExperienceSnapshot(
    string CompanyName,
    string JobTitle,
    DateOnly StartDate,
    DateOnly? EndDate);
