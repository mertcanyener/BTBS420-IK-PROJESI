namespace BTBS420.RecruitmentSystem.Web.Ai.Evaluation;

public sealed record CvAnalysisResult(
    int CandidateProfileId,
    string ProfessionalSummary,
    IReadOnlyList<CandidateExperienceSnapshot> Experiences,
    IReadOnlyList<CandidateEducationSnapshot> Educations,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> Languages);
