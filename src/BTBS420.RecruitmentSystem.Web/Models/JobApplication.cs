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
        Status = ApplicationStatuses.New;
    }

    public int Id { get; private set; }

    public int JobPostingId { get; private set; }

    public JobPosting JobPosting { get; private set; } = null!;

    public int CandidateProfileId { get; private set; }

    public CandidateProfile CandidateProfile { get; private set; } = null!;

    public string Status { get; private set; } = ApplicationStatuses.New;

    public DateTime AppliedAtUtc { get; private set; }

    public DateTime? WithdrawnAtUtc { get; private set; }

    internal void Withdraw(DateTime withdrawnAtUtc)
    {
        if (!ApplicationStatuses.CanWithdraw(Status))
        {
            throw new InvalidOperationException(
                $"'{ApplicationStatuses.GetDisplayLabel(Status)}' durumundaki bir başvuru geri çekilemez.");
        }

        Status = ApplicationStatuses.Withdrawn;
        WithdrawnAtUtc = withdrawnAtUtc;
    }
}
