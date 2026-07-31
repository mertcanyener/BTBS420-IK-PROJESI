namespace BTBS420.RecruitmentSystem.Web.Models;

public sealed class JobApplication
{
    private JobApplication()
    {
    }

    internal JobApplication(int jobPostingId, int candidateProfileId, DateTime appliedAtUtc)
    {
        JobPostingId = jobPostingId;
        CandidateProfileId = candidateProfileId;
        AppliedAtUtc = appliedAtUtc;
    }

    public int Id { get; private set; }

    public int JobPostingId { get; private set; }

    public JobPosting JobPosting { get; private set; } = null!;

    public int CandidateProfileId { get; private set; }

    public CandidateProfile CandidateProfile { get; private set; } = null!;

    public DateTime AppliedAtUtc { get; private set; }
}
