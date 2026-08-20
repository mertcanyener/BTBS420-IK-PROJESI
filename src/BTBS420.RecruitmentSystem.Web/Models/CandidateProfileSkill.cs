namespace BTBS420.RecruitmentSystem.Web.Models;

public sealed class CandidateProfileSkill
{
    private CandidateProfileSkill()
    {
    }

    internal CandidateProfileSkill(int candidateProfileId, int skillId)
    {
        CandidateProfileId = candidateProfileId;
        SkillId = skillId;
    }

    public int CandidateProfileId { get; private set; }

    public CandidateProfile CandidateProfile { get; private set; } = null!;

    public int SkillId { get; private set; }

    public Skill Skill { get; private set; } = null!;
}
