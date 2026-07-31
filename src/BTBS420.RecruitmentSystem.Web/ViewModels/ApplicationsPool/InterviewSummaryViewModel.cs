namespace BTBS420.RecruitmentSystem.Web.ViewModels.ApplicationsPool;

public sealed record InterviewSummaryViewModel(
    int Id,
    string InterviewTypeLabel,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string? OnlineMeetingLink,
    string? Location,
    string StatusLabel,
    IReadOnlyList<string> ParticipantNames);
