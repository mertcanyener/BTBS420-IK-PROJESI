namespace BTBS420.RecruitmentSystem.Web.Ai;

public sealed record AiEvaluationRequest(
    string Prompt,
    string? SystemPrompt = null,
    string? JsonSchema = null,
    int? MaxOutputTokens = null);
