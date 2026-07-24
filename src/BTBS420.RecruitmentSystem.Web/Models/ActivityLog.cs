namespace BTBS420.RecruitmentSystem.Web.Models;

public sealed class ActivityLog
{
    private ActivityLog()
    {
    }

    internal ActivityLog(
        DateTimeOffset occurredAtUtc,
        string? actorUserId,
        string actionCode,
        string? targetEntityType,
        string? targetEntityId,
        string? jobPostingId,
        string? candidateId,
        string summary)
    {
        OccurredAtUtc = occurredAtUtc;
        ActorUserId = actorUserId;
        ActionCode = actionCode;
        TargetEntityType = targetEntityType;
        TargetEntityId = targetEntityId;
        JobPostingId = jobPostingId;
        CandidateId = candidateId;
        Summary = summary;
    }

    public long Id { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public string? ActorUserId { get; private set; }

    public string ActionCode { get; private set; } = string.Empty;

    public string? TargetEntityType { get; private set; }

    public string? TargetEntityId { get; private set; }

    public string? JobPostingId { get; private set; }

    public string? CandidateId { get; private set; }

    public string Summary { get; private set; } = string.Empty;
}
