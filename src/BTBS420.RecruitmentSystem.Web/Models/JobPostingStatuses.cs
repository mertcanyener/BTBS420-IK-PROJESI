using System.Collections.Frozen;

namespace BTBS420.RecruitmentSystem.Web.Models;

public static class JobPostingStatuses
{
    public const string Draft = "draft";

    private static readonly FrozenSet<string> DefinedStatuses =
        new[] { Draft }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => DefinedStatuses;

    public static bool IsDefined(string status)
    {
        return DefinedStatuses.Contains(status);
    }
}
