namespace BTBS420.RecruitmentSystem.Web.ActivityLogging;

public sealed record ActivityLogEntry(
    string ActionCode,
    string Summary,
    string? TargetEntityType = null,
    string? TargetEntityId = null,
    string? JobPostingId = null,
    string? CandidateId = null);
