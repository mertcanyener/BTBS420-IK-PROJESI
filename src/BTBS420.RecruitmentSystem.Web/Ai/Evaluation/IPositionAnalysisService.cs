namespace BTBS420.RecruitmentSystem.Web.Ai.Evaluation;

public interface IPositionAnalysisService
{
    Task<PositionAnalysisResult> AnalyzeAsync(
        int jobPostingId,
        CancellationToken cancellationToken = default);
}
