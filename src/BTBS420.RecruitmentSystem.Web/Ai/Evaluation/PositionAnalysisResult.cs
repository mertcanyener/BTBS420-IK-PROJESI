namespace BTBS420.RecruitmentSystem.Web.Ai.Evaluation;

public sealed record PositionAnalysisResult(
    int JobPostingId,
    int PositionId,
    string PositionTitle,
    string? SeniorityLevelName,
    int? SeniorityRank,
    string RequirementsSummary,
    string RoleFamily,
    IReadOnlyList<RoleResponsibilityExpectation> ResponsibilityExpectations,
    string ImportanceRationale);
