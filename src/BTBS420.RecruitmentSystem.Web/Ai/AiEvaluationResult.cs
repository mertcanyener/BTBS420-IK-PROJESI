using System.Text.Json;

namespace BTBS420.RecruitmentSystem.Web.Ai;

public sealed record AiEvaluationResult(
    string RawText,
    JsonDocument? ParsedJson,
    AiResponseMetadata Metadata);
