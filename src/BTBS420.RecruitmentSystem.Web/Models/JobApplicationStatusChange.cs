namespace BTBS420.RecruitmentSystem.Web.Models;

public sealed class JobApplicationStatusChange
{
    public const int MaximumReasonLength = 1000;

    private JobApplicationStatusChange()
    {
    }

    internal JobApplicationStatusChange(
        int jobApplicationId,
        string fromStatus,
        string toStatus,
        string actorUserId,
        string? reason,
        DateTime changedAtUtc)
    {
        JobApplicationId = jobApplicationId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ActorUserId = actorUserId;
        Reason = NormalizeReason(reason);
        ChangedAtUtc = changedAtUtc;
    }

    public int Id { get; private set; }

    public int JobApplicationId { get; private set; }

    public JobApplication JobApplication { get; private set; } = null!;

    public string FromStatus { get; private set; } = string.Empty;

    public string ToStatus { get; private set; } = string.Empty;

    public string ActorUserId { get; private set; } = string.Empty;

    public string? Reason { get; private set; }

    public DateTime ChangedAtUtc { get; private set; }

    private static string? NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        var normalized = reason.Trim();

        if (normalized.Length > MaximumReasonLength)
        {
            throw new ArgumentException(
                $"Gerekçe en fazla {MaximumReasonLength} karakter olabilir.",
                nameof(reason));
        }

        return normalized;
    }
}
