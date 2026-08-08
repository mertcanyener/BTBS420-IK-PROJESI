namespace BTBS420.RecruitmentSystem.Web.Ai.Evaluation;

public interface ICvAnalysisService
{
    Task<CvAnalysisResult> AnalyzeAsync(
        int candidateProfileId,
        CancellationToken cancellationToken = default);
}
