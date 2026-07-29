namespace BTBS420.RecruitmentSystem.Web.Models;

public sealed class Department
{
    public const int MaximumNameLength = 200;

    private Department()
    {
    }

    internal Department(string name)
    {
        Name = NormalizeName(name);
        IsActive = true;
    }

    public int Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    internal void Rename(string name)
    {
        Name = NormalizeName(name);
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
            throw new ArgumentException("Departman adı boş olamaz.", nameof(name));
        }

        var normalized = name.Trim();

        if (normalized.Length > MaximumNameLength)
        {
            throw new ArgumentException(
                $"Departman adı en fazla {MaximumNameLength} karakter olabilir.",
                nameof(name));
        }

        return normalized;
    }
}
