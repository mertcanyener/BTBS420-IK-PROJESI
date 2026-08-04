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

    public byte[] RowVersion { get; private set; } = [];

    internal JobApplicationStatusChange Withdraw(string actorUserId, DateTime withdrawnAtUtc)
    {
        return TransitionTo(ApplicationStatuses.Withdrawn, actorUserId, reason: null, withdrawnAtUtc);
    }

    internal JobApplicationStatusChange TransitionTo(
        string newStatus,
        string actorUserId,
        string? reason,
        DateTime changedAtUtc)
    {
        if (!ApplicationStatuses.IsValidTransition(Status, newStatus))
        {
            throw new InvalidOperationException(
                $"'{ApplicationStatuses.GetDisplayLabel(Status)}' durumundaki bir başvuru " +
                $"'{ApplicationStatuses.GetDisplayLabel(newStatus)}' durumuna geçemez.");
        }

        var change = new JobApplicationStatusChange(
            Id,
            Status,
            newStatus,
            actorUserId,
            reason,
            changedAtUtc);

        Status = newStatus;
        if (newStatus == ApplicationStatuses.Withdrawn)
        {
            WithdrawnAtUtc = changedAtUtc;
        }

        return change;
    }
}
