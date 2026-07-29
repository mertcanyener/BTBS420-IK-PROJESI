namespace BTBS420.RecruitmentSystem.Web.Models;

public sealed class JobPosting
{
    public const int MaximumTitleLength = 300;
    public const int MaximumDescriptionLength = 4000;

    private JobPosting()
    {
    }

    internal JobPosting(
        string title,
        string description,
        int positionId,
        string responsibleUserId,
        DateOnly applicationDeadline,
        DateOnly today)
    {
        Title = NormalizeTitle(title);
        Description = NormalizeDescription(description);
        PositionId = positionId;
        ResponsibleUserId = NormalizeResponsibleUserId(responsibleUserId);
        ApplicationDeadline = ValidateDeadline(applicationDeadline, today);
        Status = JobPostingStatuses.Draft;
    }

    public int Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int PositionId { get; private set; }

    public Position Position { get; private set; } = null!;

    public string ResponsibleUserId { get; private set; } = string.Empty;

    public ApplicationUser ResponsibleUser { get; private set; } = null!;

    public DateOnly ApplicationDeadline { get; private set; }

    public string Status { get; private set; } = JobPostingStatuses.Draft;

    public byte[] RowVersion { get; private set; } = [];

    internal void ChangeStatus(string newStatus)
    {
        if (!JobPostingStatuses.IsValidTransition(Status, newStatus))
        {
            throw new InvalidOperationException(
                $"İlan '{Status}' durumundan '{newStatus}' durumuna geçemez.");
        }

        Status = newStatus;
    }

    internal void Edit(
        string title,
        string description,
        int positionId,
        string responsibleUserId,
        DateOnly applicationDeadline,
        DateOnly today)
    {
        Title = NormalizeTitle(title);
        Description = NormalizeDescription(description);
        PositionId = positionId;
        ResponsibleUserId = NormalizeResponsibleUserId(responsibleUserId);
        ApplicationDeadline = ValidateDeadline(applicationDeadline, today);
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("İlan başlığı boş olamaz.", nameof(title));
        }

        var normalized = title.Trim();

        if (normalized.Length > MaximumTitleLength)
        {
            throw new ArgumentException(
                $"İlan başlığı en fazla {MaximumTitleLength} karakter olabilir.",
                nameof(title));
        }

        return normalized;
    }

    private static string NormalizeDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("İlan açıklaması boş olamaz.", nameof(description));
        }

        var normalized = description.Trim();

        if (normalized.Length > MaximumDescriptionLength)
        {
            throw new ArgumentException(
                $"İlan açıklaması en fazla {MaximumDescriptionLength} karakter olabilir.",
                nameof(description));
        }

        return normalized;
    }

    private static string NormalizeResponsibleUserId(string responsibleUserId)
    {
        if (string.IsNullOrWhiteSpace(responsibleUserId))
        {
            throw new ArgumentException(
                "Sorumlu uzman seçimi zorunludur.",
                nameof(responsibleUserId));
        }

        return responsibleUserId;
    }

    private static DateOnly ValidateDeadline(DateOnly applicationDeadline, DateOnly today)
    {
        if (applicationDeadline <= today)
        {
            throw new ArgumentException(
                "Son başvuru tarihi bugünden ileri bir tarih olmalıdır.",
                nameof(applicationDeadline));
        }

        return applicationDeadline;
    }
}
