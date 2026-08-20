namespace BTBS420.RecruitmentSystem.Web.Models;

public sealed class ApplicationNote
{
    public const int MaximumBodyLength = 2000;

    private ApplicationNote()
    {
    }

    internal ApplicationNote(
        int jobApplicationId,
        string authorUserId,
        string body,
        DateTime createdAtUtc)
    {
        JobApplicationId = jobApplicationId;
        AuthorUserId = NormalizeAuthorUserId(authorUserId);
        Body = NormalizeBody(body);
        CreatedAtUtc = createdAtUtc;
    }

    public int Id { get; private set; }

    public int JobApplicationId { get; private set; }

    public JobApplication JobApplication { get; private set; } = null!;

    public string AuthorUserId { get; private set; } = string.Empty;

    public ApplicationUser AuthorUser { get; private set; } = null!;

    public string Body { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    private static string NormalizeAuthorUserId(string authorUserId)
    {
        if (string.IsNullOrWhiteSpace(authorUserId))
        {
            throw new ArgumentException("Not yazarı kimliği boş olamaz.", nameof(authorUserId));
        }

        return authorUserId;
    }

    private static string NormalizeBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("Not metni boş olamaz.", nameof(body));
        }

        var normalized = body.Trim();

        if (normalized.Length > MaximumBodyLength)
        {
            throw new ArgumentException(
                $"Not metni en fazla {MaximumBodyLength} karakter olabilir.",
                nameof(body));
        }

        return normalized;
    }
}
