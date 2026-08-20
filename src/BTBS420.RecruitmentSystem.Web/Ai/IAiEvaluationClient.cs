namespace BTBS420.RecruitmentSystem.Web.Ai;

public interface IAiEvaluationClient
{
    Task<AiEvaluationResult> EvaluateAsync(
        AiEvaluationRequest request,
        CancellationToken cancellationToken = default);
}
