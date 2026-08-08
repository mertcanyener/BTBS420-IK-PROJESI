namespace BTBS420.RecruitmentSystem.Web.Ai;

public sealed class NoOpAiEvaluationClient : IAiEvaluationClient
{
    public Task<AiEvaluationResult> EvaluateAsync(
        AiEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var metadata = new AiResponseMetadata(
            Provider: "None",
            Model: "none",
            ModelVersion: null,
            InputTokens: 0,
            OutputTokens: 0,
            LatencyMs: 0);

        var result = new AiEvaluationResult(
            RawText: string.Empty,
            ParsedJson: null,
            Metadata: metadata);

        return Task.FromResult(result);
    }
}
