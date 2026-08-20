using System.Collections.Frozen;

namespace BTBS420.RecruitmentSystem.Web.Models;

public static class InterviewTypes
{
    public const string Online = "online";
    public const string InPerson = "in-person";

    private static readonly FrozenSet<string> DefinedTypes =
        new[] { Online, InPerson }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => DefinedTypes;

    public static bool IsDefined(string interviewType)
    {
        return DefinedTypes.Contains(interviewType);
    }

    public static string GetDisplayLabel(string interviewType)
    {
        return interviewType switch
        {
            Online => "Çevrimiçi",
            InPerson => "Yüz Yüze",
            _ => interviewType
        };
    }
}
