namespace BTBS420.RecruitmentSystem.Web.Ai.Evaluation;

/// <summary>
/// AI tarafından üretilen bir aday-pozisyon değerlendirme önerisidir. İşe alım kararı
/// değildir ve hiçbir başvuru/mülakat durumunu otomatik değiştirmez — nihai karar her
/// zaman yetkili kullanıcıya aittir.
/// </summary>
public sealed record CandidatePositionEvaluation(
    int CandidateProfileId,
    int JobPostingId,
    double OverallScore,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Gaps,
    string ReasoningSummary,
    IReadOnlyList<EvaluationEvidenceItem> Evidence,
    AiResponseMetadata Metadata,
    string PromptVersion,
    DateTimeOffset EvaluatedAtUtc);
