using System.Collections.Frozen;

namespace BTBS420.RecruitmentSystem.Web.ActivityLogging;

public static class ActivityEntityTypes
{
    public const string System = "system";
    public const string User = "user";
    public const string Role = "role";
    public const string Department = "department";
    public const string JobFamily = "job-family";
    public const string Seniority = "seniority";
    public const string Position = "position";
    public const string Skill = "skill";
    public const string Education = "education";
    public const string Language = "language";
    public const string Location = "location";
    public const string ExperienceRange = "experience-range";
    public const string JobPosting = "job-posting";
    public const string Candidate = "candidate";
    public const string Application = "application";
    public const string Interview = "interview";
    public const string Offer = "offer";
    public const string Notification = "notification";

    private static readonly FrozenSet<string> DefinedTypes =
        new[]
        {
            System,
            User,
            Role,
            Department,
            JobFamily,
            Seniority,
            Position,
            Skill,
            Education,
            Language,
            Location,
            ExperienceRange,
            JobPosting,
            Candidate,
            Application,
            Interview,
            Offer,
            Notification
        }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => DefinedTypes;

    public static bool IsDefined(string entityType)
    {
        return DefinedTypes.Contains(entityType);
    }
}
