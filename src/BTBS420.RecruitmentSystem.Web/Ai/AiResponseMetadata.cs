namespace BTBS420.RecruitmentSystem.Web.Ai;

public sealed record AiResponseMetadata(
    string Provider,
    string Model,
    string? ModelVersion,
    int InputTokens,
    int OutputTokens,
    long LatencyMs);
