namespace BTBS420.RecruitmentSystem.Web.Ai.Evaluation;

public interface ICandidatePositionEvaluationService
{
    Task<CandidatePositionEvaluation> EvaluateAsync(
        CvAnalysisResult candidate,
        PositionAnalysisResult position,
        CancellationToken cancellationToken = default);
}
