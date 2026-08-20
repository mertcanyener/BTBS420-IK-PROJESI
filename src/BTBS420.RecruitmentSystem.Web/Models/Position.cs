namespace BTBS420.RecruitmentSystem.Web.Models;

public sealed class Position
{
    public const int MaximumNameLength = 200;

    private Position()
    {
    }

    internal Position(string name, int departmentId, int? jobFamilyId, int? seniorityId)
    {
        Name = NormalizeName(name);
        DepartmentId = departmentId;
        JobFamilyId = jobFamilyId;
        SeniorityId = seniorityId;
        IsActive = true;
    }

    public int Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int DepartmentId { get; private set; }

    public Department Department { get; private set; } = null!;

    public int? JobFamilyId { get; private set; }

    public JobFamily? JobFamily { get; private set; }

    public int? SeniorityId { get; private set; }

    public Seniority? Seniority { get; private set; }

    public bool IsActive { get; private set; }

    internal void Rename(string name)
    {
        Name = NormalizeName(name);
    }

    internal void ChangeAssociations(int departmentId, int? jobFamilyId, int? seniorityId)
    {
        DepartmentId = departmentId;
        JobFamilyId = jobFamilyId;
        SeniorityId = seniorityId;
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
            throw new ArgumentException("Pozisyon adı boş olamaz.", nameof(name));
        }

        var normalized = name.Trim();

        if (normalized.Length > MaximumNameLength)
        {
            throw new ArgumentException(
                $"Pozisyon adı en fazla {MaximumNameLength} karakter olabilir.",
                nameof(name));
        }

        return normalized;
    }
}
