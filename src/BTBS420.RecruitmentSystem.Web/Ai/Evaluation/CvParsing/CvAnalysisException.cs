namespace BTBS420.RecruitmentSystem.Web.Ai.Evaluation.CvParsing;

public sealed class CvAnalysisException : Exception
{
    public CvAnalysisException(CvAnalysisFailureReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public CvAnalysisFailureReason Reason { get; }
}
