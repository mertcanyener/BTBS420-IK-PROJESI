namespace BTBS420.RecruitmentSystem.Web.ViewModels.Interviews;

public sealed record InterviewDetailViewModel(
    int Id,
    string CandidateFullName,
    string JobPostingTitle,
    string PositionName,
    string DepartmentName,
    string InterviewTypeLabel,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string? OnlineMeetingLink,
    string? Location,
    string StatusLabel,
    IReadOnlyList<string> ParticipantNames,
    bool CanEdit,
    bool CanViewEvaluationSummary,
    IReadOnlyList<InterviewEvaluationSummaryItemViewModel> EvaluationSummary,
    double? AverageCompetencyScore,
    double? AverageOverallScore,
    bool CanComplete,
    bool CanCancel,
    bool CanAddEvaluation,
    bool HasEvaluated);
