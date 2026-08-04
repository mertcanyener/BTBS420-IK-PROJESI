using System.Collections.Frozen;

namespace BTBS420.RecruitmentSystem.Web.Models;

public static class OfferStatuses
{
    public const string Draft = "draft";

    private static readonly FrozenSet<string> DefinedStatuses =
        new[] { Draft }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => DefinedStatuses;

    public static bool IsDefined(string status)
    {
        return DefinedStatuses.Contains(status);
    }

    public static string GetDisplayLabel(string status)
    {
        return status switch
        {
            Draft => "Taslak",
            _ => status
        };
    }
}
