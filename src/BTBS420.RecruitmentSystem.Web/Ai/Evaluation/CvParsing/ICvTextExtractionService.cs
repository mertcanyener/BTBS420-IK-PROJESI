namespace BTBS420.RecruitmentSystem.Web.Ai.Evaluation.CvParsing;

public interface ICvTextExtractionService
{
    Task<CvTextExtractionResult> ExtractAsync(
        Stream content,
        string fileExtension,
        CancellationToken cancellationToken = default);
}
