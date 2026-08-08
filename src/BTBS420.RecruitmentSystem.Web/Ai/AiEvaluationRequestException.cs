namespace BTBS420.RecruitmentSystem.Web.Ai;

public sealed class AiEvaluationRequestException : AiEvaluationException
{
    public AiEvaluationRequestException(string message)
        : base(message)
    {
    }

    public AiEvaluationRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
