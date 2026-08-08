namespace BTBS420.RecruitmentSystem.Web.Ai;

public sealed class AiEvaluationTransientException : AiEvaluationException
{
    public AiEvaluationTransientException(string message)
        : base(message)
    {
    }

    public AiEvaluationTransientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
