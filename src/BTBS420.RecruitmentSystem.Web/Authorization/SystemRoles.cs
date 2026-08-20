namespace BTBS420.RecruitmentSystem.Web.Authorization;

public static class SystemRoles
{
    public const string Admin = "Admin";
    public const string RecruitmentSpecialist = "İşe Alım Uzmanı";
    public const string HiringManager = "İşe Alım Yöneticisi";
    public const string Candidate = "Aday";

    public static IReadOnlyList<string> All { get; } =
        Array.AsReadOnly(
        new[]
        {
            Admin,
            RecruitmentSpecialist,
            HiringManager,
            Candidate
        });
}
