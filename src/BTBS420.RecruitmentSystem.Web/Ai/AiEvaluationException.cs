namespace BTBS420.RecruitmentSystem.Web.Ai;

public class AiEvaluationException : Exception
{
    public AiEvaluationException(string message)
        : base(message)
    {
    }

    public AiEvaluationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
