namespace BTBS420.RecruitmentSystem.Web.Ai.Evaluation;

/// <summary>
/// Sıralama, adayların bağımsız olarak üretilmiş değerlendirmeleri üzerinde sonradan
/// yapılan ayrı bir adımdır; her aday diğerlerinden bağımsız değerlendirilir, sıralama
/// bu değerlendirmeler üzerinde ayrıca hesaplanır.
/// </summary>
public interface ICandidateRankingService
{
    IReadOnlyList<CandidatePositionEvaluation> Rank(
        IReadOnlyList<CandidatePositionEvaluation> evaluations);
}
