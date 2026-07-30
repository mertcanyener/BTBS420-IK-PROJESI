namespace BTBS420.RecruitmentSystem.Web.Models;

public sealed class CandidateExperience
{
    public const int MaximumCompanyNameLength = 200;
    public const int MaximumJobTitleLength = 200;

    private CandidateExperience()
    {
    }

    internal CandidateExperience(
        int candidateProfileId,
        string companyName,
        string jobTitle,
        DateOnly startDate,
        DateOnly? endDate)
    {
        CandidateProfileId = candidateProfileId;
        CompanyName = NormalizeCompanyName(companyName);
        JobTitle = NormalizeJobTitle(jobTitle);
        (StartDate, EndDate) = ValidateDates(startDate, endDate);
    }

    public int Id { get; private set; }

    public int CandidateProfileId { get; private set; }

    public CandidateProfile CandidateProfile { get; private set; } = null!;

    public string CompanyName { get; private set; } = string.Empty;

    public string JobTitle { get; private set; } = string.Empty;

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    internal void Edit(
        string companyName,
        string jobTitle,
        DateOnly startDate,
        DateOnly? endDate)
    {
        CompanyName = NormalizeCompanyName(companyName);
        JobTitle = NormalizeJobTitle(jobTitle);
        (StartDate, EndDate) = ValidateDates(startDate, endDate);
    }

    private static string NormalizeCompanyName(string companyName)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            throw new ArgumentException("Şirket adı boş olamaz.", nameof(companyName));
        }

        var normalized = companyName.Trim();

        if (normalized.Length > MaximumCompanyNameLength)
        {
            throw new ArgumentException(
                $"Şirket adı en fazla {MaximumCompanyNameLength} karakter olabilir.",
                nameof(companyName));
        }

        return normalized;
    }

    private static string NormalizeJobTitle(string jobTitle)
    {
        if (string.IsNullOrWhiteSpace(jobTitle))
        {
            throw new ArgumentException("Unvan boş olamaz.", nameof(jobTitle));
        }

        var normalized = jobTitle.Trim();

        if (normalized.Length > MaximumJobTitleLength)
        {
            throw new ArgumentException(
                $"Unvan en fazla {MaximumJobTitleLength} karakter olabilir.",
                nameof(jobTitle));
        }

        return normalized;
    }

    private static (DateOnly StartDate, DateOnly? EndDate) ValidateDates(
        DateOnly startDate,
        DateOnly? endDate)
    {
        if (endDate.HasValue && endDate.Value < startDate)
        {
            throw new ArgumentException(
                "Bitiş tarihi, başlangıç tarihinden önce olamaz.",
                nameof(endDate));
        }

        return (startDate, endDate);
    }
}
