namespace BTBS420.RecruitmentSystem.Web.ViewModels.Interviews;

public sealed record PendingEvaluationListItemViewModel(
    int InterviewId,
    string CandidateFullName,
    string JobPostingTitle,
    DateTime StartAtUtc,
    IReadOnlyList<string> MissingEvaluatorNames);
