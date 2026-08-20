namespace BTBS420.RecruitmentSystem.Web.Models;

public sealed class CandidateEducation
{
    public const int MaximumSchoolNameLength = 200;
    public const int MaximumFieldOfStudyLength = 200;

    private CandidateEducation()
    {
    }

    internal CandidateEducation(
        int candidateProfileId,
        int educationId,
        string schoolName,
        string? fieldOfStudy,
        DateOnly startDate,
        DateOnly? endDate)
    {
        CandidateProfileId = candidateProfileId;
        EducationId = educationId;
        SchoolName = NormalizeSchoolName(schoolName);
        FieldOfStudy = NormalizeFieldOfStudy(fieldOfStudy);
        (StartDate, EndDate) = ValidateDates(startDate, endDate);
    }

    public int Id { get; private set; }

    public int CandidateProfileId { get; private set; }

    public CandidateProfile CandidateProfile { get; private set; } = null!;

    public int EducationId { get; private set; }

    public Education Education { get; private set; } = null!;

    public string SchoolName { get; private set; } = string.Empty;

    public string? FieldOfStudy { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    internal void Edit(
        int educationId,
        string schoolName,
        string? fieldOfStudy,
        DateOnly startDate,
        DateOnly? endDate)
    {
        EducationId = educationId;
        SchoolName = NormalizeSchoolName(schoolName);
        FieldOfStudy = NormalizeFieldOfStudy(fieldOfStudy);
        (StartDate, EndDate) = ValidateDates(startDate, endDate);
    }

    private static string NormalizeSchoolName(string schoolName)
    {
        if (string.IsNullOrWhiteSpace(schoolName))
        {
            throw new ArgumentException("Okul adı boş olamaz.", nameof(schoolName));
        }

        var normalized = schoolName.Trim();

        if (normalized.Length > MaximumSchoolNameLength)
        {
            throw new ArgumentException(
                $"Okul adı en fazla {MaximumSchoolNameLength} karakter olabilir.",
                nameof(schoolName));
        }

        return normalized;
    }

    private static string? NormalizeFieldOfStudy(string? fieldOfStudy)
    {
        if (string.IsNullOrWhiteSpace(fieldOfStudy))
        {
            return null;
        }

        var normalized = fieldOfStudy.Trim();

        if (normalized.Length > MaximumFieldOfStudyLength)
        {
            throw new ArgumentException(
                $"Bölüm en fazla {MaximumFieldOfStudyLength} karakter olabilir.",
                nameof(fieldOfStudy));
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
