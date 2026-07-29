namespace BTBS420.RecruitmentSystem.Web.Models;

public sealed class CandidateProfileLanguage
{
    private CandidateProfileLanguage()
    {
    }

    internal CandidateProfileLanguage(int candidateProfileId, int languageId)
    {
        CandidateProfileId = candidateProfileId;
        LanguageId = languageId;
    }

    public int CandidateProfileId { get; private set; }

    public CandidateProfile CandidateProfile { get; private set; } = null!;

    public int LanguageId { get; private set; }

    public Language Language { get; private set; } = null!;
}
