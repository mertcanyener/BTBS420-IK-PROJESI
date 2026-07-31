namespace BTBS420.RecruitmentSystem.Web.Models;

public sealed class Interview
{
    public const int MaximumOnlineMeetingLinkLength = 500;
    public const int MaximumLocationLength = 300;

    private Interview()
    {
    }

    internal Interview(
        int jobApplicationId,
        string interviewType,
        DateTime startAtUtc,
        DateTime endAtUtc,
        string? onlineMeetingLink,
        string? location)
    {
        JobApplicationId = jobApplicationId;
        InterviewType = NormalizeInterviewType(interviewType);
        (StartAtUtc, EndAtUtc) = ValidateTimeRange(startAtUtc, endAtUtc);
        OnlineMeetingLink = NormalizeOnlineMeetingLink(InterviewType, onlineMeetingLink);
        Location = NormalizeLocation(InterviewType, location);
        Status = InterviewStatuses.Scheduled;
    }

    public int Id { get; private set; }

    public int JobApplicationId { get; private set; }

    public JobApplication JobApplication { get; private set; } = null!;

    public string InterviewType { get; private set; } = string.Empty;

    public DateTime StartAtUtc { get; private set; }

    public DateTime EndAtUtc { get; private set; }

    public string? OnlineMeetingLink { get; private set; }

    public string? Location { get; private set; }

    public string Status { get; private set; } = InterviewStatuses.Scheduled;

    private static string NormalizeInterviewType(string interviewType)
    {
        if (!InterviewTypes.IsDefined(interviewType))
        {
            throw new ArgumentException("Geçersiz mülakat türü.", nameof(interviewType));
        }

        return interviewType;
    }

    private static (DateTime StartAtUtc, DateTime EndAtUtc) ValidateTimeRange(
        DateTime startAtUtc,
        DateTime endAtUtc)
    {
        if (endAtUtc <= startAtUtc)
        {
            throw new ArgumentException(
                "Bitiş zamanı, başlangıç zamanından sonra olmalıdır.",
                nameof(endAtUtc));
        }

        return (startAtUtc, endAtUtc);
    }

    private static string? NormalizeOnlineMeetingLink(string interviewType, string? onlineMeetingLink)
    {
        if (interviewType != InterviewTypes.Online)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(onlineMeetingLink))
        {
            throw new ArgumentException(
                "Çevrimiçi mülakatlar için toplantı linki zorunludur.",
                nameof(onlineMeetingLink));
        }

        var normalized = onlineMeetingLink.Trim();

        if (normalized.Length > MaximumOnlineMeetingLinkLength)
        {
            throw new ArgumentException(
                $"Toplantı linki en fazla {MaximumOnlineMeetingLinkLength} karakter olabilir.",
                nameof(onlineMeetingLink));
        }

        return normalized;
    }

    private static string? NormalizeLocation(string interviewType, string? location)
    {
        if (interviewType != InterviewTypes.InPerson)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException(
                "Yüz yüze mülakatlar için konum zorunludur.",
                nameof(location));
        }

        var normalized = location.Trim();

        if (normalized.Length > MaximumLocationLength)
        {
            throw new ArgumentException(
                $"Konum en fazla {MaximumLocationLength} karakter olabilir.",
                nameof(location));
        }

        return normalized;
    }
}
