namespace BTBS420.RecruitmentSystem.Web.Models;

public sealed class InterviewEvaluation
{
    public const int MinimumScore = 1;
    public const int MaximumScore = 5;
    public const int MaximumNoteLength = 2000;

    private InterviewEvaluation()
    {
    }

    internal InterviewEvaluation(
        int interviewId,
        string evaluatorUserId,
        string? note,
        int competencyScore,
        int overallScore,
        string recommendation,
        DateTime createdAtUtc)
    {
        InterviewId = interviewId;
        EvaluatorUserId = NormalizeEvaluatorUserId(evaluatorUserId);
        CreatedAtUtc = createdAtUtc;
        Edit(note, competencyScore, overallScore, recommendation);
    }

    public int Id { get; private set; }

    public int InterviewId { get; private set; }

    public Interview Interview { get; private set; } = null!;

    public string EvaluatorUserId { get; private set; } = string.Empty;

    public string? Note { get; private set; }

    public int CompetencyScore { get; private set; }

    public int OverallScore { get; private set; }

    public string Recommendation { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    internal void Edit(string? note, int competencyScore, int overallScore, string recommendation)
    {
        Note = NormalizeNote(note);
        CompetencyScore = ValidateScore(competencyScore, nameof(competencyScore));
        OverallScore = ValidateScore(overallScore, nameof(overallScore));
        Recommendation = ValidateRecommendation(recommendation);
    }

    private static string NormalizeEvaluatorUserId(string evaluatorUserId)
    {
        if (string.IsNullOrWhiteSpace(evaluatorUserId))
        {
            throw new ArgumentException("Değerlendiren kullanıcı kimliği boş olamaz.", nameof(evaluatorUserId));
        }

        return evaluatorUserId;
    }

    private static int ValidateScore(int score, string paramName)
    {
        if (score < MinimumScore || score > MaximumScore)
        {
            throw new ArgumentException(
                $"Puan {MinimumScore} ile {MaximumScore} arasında olmalıdır.",
                paramName);
        }

        return score;
    }

    private static string ValidateRecommendation(string recommendation)
    {
        if (!InterviewEvaluationRecommendations.IsDefined(recommendation))
        {
            throw new ArgumentException("Geçersiz öneri değeri.", nameof(recommendation));
        }

        return recommendation;
    }

    private static string? NormalizeNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        var normalized = note.Trim();

        if (normalized.Length > MaximumNoteLength)
        {
            throw new ArgumentException(
                $"Not en fazla {MaximumNoteLength} karakter olabilir.",
                nameof(note));
        }

        return normalized;
    }
}
