namespace BTBS420.RecruitmentSystem.Web.Ai.Evaluation;

public static class EvaluationEvidenceGroundingValidator
{
    public static readonly IReadOnlyCollection<string> AllowedSourceFields = new[]
    {
        "CandidateProfile.ProfessionalSummary",
        "CandidateExperience.CompanyName",
        "CandidateExperience.JobTitle",
        "CandidateEducation.SchoolName",
        "CandidateEducation.FieldOfStudy",
        "CandidateEducation.Education",
        "CandidateProfileSkill.Skill",
        "CandidateProfileLanguage.Language",
    };

    public static void EnsureGrounded(IEnumerable<EvaluationEvidenceItem> evidence)
    {
        foreach (var item in evidence)
        {
            if (!AllowedSourceFields.Contains(item.SourceField))
            {
                throw new AiEvaluationRequestException(
                    $"Kanıt kaynağı '{item.SourceField}' bilinen bir aday alanına karşılık gelmiyor.");
            }

            if (string.IsNullOrWhiteSpace(item.SourceExcerpt))
            {
                throw new AiEvaluationRequestException(
                    $"'{item.SourceField}' alanı için kanıt metni boş olamaz.");
            }
        }
    }
}
