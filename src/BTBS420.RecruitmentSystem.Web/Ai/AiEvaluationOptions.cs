namespace BTBS420.RecruitmentSystem.Web.Ai;

public sealed class AiEvaluationOptions
{
    public const string SectionName = "AiEvaluation";

    public const string ApiKeyConfigurationKey = "AiEvaluation:ApiKey";

    public const string ApiKeyEnvironmentVariable = "AiEvaluation__ApiKey";

    public string Provider { get; set; } = "None";

    public string Model { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;

    public int MaxRetries { get; set; } = 2;
}
