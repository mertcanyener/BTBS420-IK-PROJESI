namespace BTBS420.RecruitmentSystem.Web.ViewModels.Interviews;

public sealed record InterviewEvaluationSummaryItemViewModel(
    string EvaluatorName,
    string? Note,
    int CompetencyScore,
    int OverallScore,
    string RecommendationLabel);
