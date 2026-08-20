namespace BTBS420.RecruitmentSystem.Web.Models;

public sealed class ExperienceRange
{
    public const int MaximumNameLength = 200;

    private ExperienceRange()
    {
    }

    internal ExperienceRange(string name, int minYears, int maxYears)
    {
        Name = NormalizeName(name);
        SetRange(minYears, maxYears);
        IsActive = true;
    }

    public int Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int MinYears { get; private set; }

    public int MaxYears { get; private set; }

    public bool IsActive { get; private set; }

    internal void Rename(string name)
    {
        Name = NormalizeName(name);
    }

    internal void ChangeRange(int minYears, int maxYears)
    {
        SetRange(minYears, maxYears);
    }

    internal void Deactivate()
    {
        IsActive = false;
    }

    internal void Activate()
    {
        IsActive = true;
    }

    private void SetRange(int minYears, int maxYears)
    {
        if (minYears < 0)
        {
            throw new ArgumentException(
                "Minimum deneyim yılı negatif olamaz.",
                nameof(minYears));
        }

        if (maxYears < minYears)
        {
            throw new ArgumentException(
                "Maksimum deneyim yılı, minimumdan küçük olamaz.",
                nameof(maxYears));
        }

        MinYears = minYears;
        MaxYears = maxYears;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Deneyim aralığı adı boş olamaz.", nameof(name));
        }

        var normalized = name.Trim();

        if (normalized.Length > MaximumNameLength)
        {
            throw new ArgumentException(
                $"Deneyim aralığı adı en fazla {MaximumNameLength} karakter olabilir.",
                nameof(name));
        }

        return normalized;
    }
}
