namespace BTBS420.RecruitmentSystem.Web.Models;

public sealed class Seniority
{
    public const int MaximumNameLength = 200;

    private Seniority()
    {
    }

    internal Seniority(string name, int rank)
    {
        Name = NormalizeName(name);
        Rank = rank;
        IsActive = true;
    }

    public int Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int Rank { get; private set; }

    public bool IsActive { get; private set; }

    internal void Rename(string name)
    {
        Name = NormalizeName(name);
    }

    internal void ChangeRank(int rank)
    {
        Rank = rank;
    }

    internal void Deactivate()
    {
        IsActive = false;
    }

    internal void Activate()
    {
        IsActive = true;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Kıdem adı boş olamaz.", nameof(name));
        }

        var normalized = name.Trim();

        if (normalized.Length > MaximumNameLength)
        {
            throw new ArgumentException(
                $"Kıdem adı en fazla {MaximumNameLength} karakter olabilir.",
                nameof(name));
        }

        return normalized;
    }
}
