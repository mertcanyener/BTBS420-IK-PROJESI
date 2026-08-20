namespace BTBS420.RecruitmentSystem.Web.Models;

public sealed class CandidateProfile
{
    public const int MaximumFirstNameLength = 100;
    public const int MaximumLastNameLength = 100;
    public const int MaximumProfessionalSummaryLength = 2000;

    private CandidateProfile()
    {
    }

    internal CandidateProfile(
        string applicationUserId,
        string firstName,
        string lastName,
        string? professionalSummary,
        int? targetPositionId)
    {
        ApplicationUserId = NormalizeApplicationUserId(applicationUserId);
        FirstName = NormalizeFirstName(firstName);
        LastName = NormalizeLastName(lastName);
        ProfessionalSummary = NormalizeProfessionalSummary(professionalSummary);
        TargetPositionId = targetPositionId;
    }

    public int Id { get; private set; }

    public string ApplicationUserId { get; private set; } = string.Empty;

    public ApplicationUser ApplicationUser { get; private set; } = null!;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string ProfessionalSummary { get; private set; } = string.Empty;

    public int? TargetPositionId { get; private set; }

    public Position? TargetPosition { get; private set; }

    internal void Edit(
        string firstName,
        string lastName,
        string? professionalSummary,
        int? targetPositionId)
    {
        FirstName = NormalizeFirstName(firstName);
        LastName = NormalizeLastName(lastName);
        ProfessionalSummary = NormalizeProfessionalSummary(professionalSummary);
        TargetPositionId = targetPositionId;
    }

    private static string NormalizeApplicationUserId(string applicationUserId)
    {
        if (string.IsNullOrWhiteSpace(applicationUserId))
        {
            throw new ArgumentException("Kullanıcı kimliği boş olamaz.", nameof(applicationUserId));
        }

        return applicationUserId;
    }

    private static string NormalizeFirstName(string firstName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new ArgumentException("Ad boş olamaz.", nameof(firstName));
        }

        var normalized = firstName.Trim();

        if (normalized.Length > MaximumFirstNameLength)
        {
            throw new ArgumentException(
                $"Ad en fazla {MaximumFirstNameLength} karakter olabilir.",
                nameof(firstName));
        }

        return normalized;
    }

    private static string NormalizeLastName(string lastName)
    {
        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new ArgumentException("Soyad boş olamaz.", nameof(lastName));
        }

        var normalized = lastName.Trim();

        if (normalized.Length > MaximumLastNameLength)
        {
            throw new ArgumentException(
                $"Soyad en fazla {MaximumLastNameLength} karakter olabilir.",
                nameof(lastName));
        }

        return normalized;
    }

    private static string NormalizeProfessionalSummary(string? professionalSummary)
    {
        if (string.IsNullOrWhiteSpace(professionalSummary))
        {
            return string.Empty;
        }

        var normalized = professionalSummary.Trim();

        if (normalized.Length > MaximumProfessionalSummaryLength)
        {
            throw new ArgumentException(
                $"Mesleki özet en fazla {MaximumProfessionalSummaryLength} karakter olabilir.",
                nameof(professionalSummary));
        }

        return normalized;
    }
}
