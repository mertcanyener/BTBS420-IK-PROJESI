using System.Collections.Frozen;

namespace BTBS420.RecruitmentSystem.Web.Models;

public static class ApplicationStatuses
{
    public const string New = "new";
    public const string Screening = "screening";
    public const string Interview = "interview";
    public const string Withdrawn = "withdrawn";

    private static readonly FrozenSet<string> DefinedStatuses =
        new[] { New, Screening, Interview, Withdrawn }
            .ToFrozenSet(StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, FrozenSet<string>> AllowedTransitions =
        new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal)
        {
            [New] = new[] { Withdrawn }.ToFrozenSet(StringComparer.Ordinal),
            [Screening] = new[] { Withdrawn }.ToFrozenSet(StringComparer.Ordinal),
            [Interview] = new[] { Withdrawn }.ToFrozenSet(StringComparer.Ordinal),
            [Withdrawn] = FrozenSet<string>.Empty
        };

    public static IReadOnlySet<string> All => DefinedStatuses;

    public static bool IsDefined(string status)
    {
        return DefinedStatuses.Contains(status);
    }

    public static bool IsValidTransition(string fromStatus, string toStatus)
    {
        return AllowedTransitions.TryGetValue(fromStatus, out var allowedTargets) &&
            allowedTargets.Contains(toStatus);
    }

    public static bool CanWithdraw(string status)
    {
        return IsValidTransition(status, Withdrawn);
    }

    public static string GetDisplayLabel(string status)
    {
        return status switch
        {
            New => "Yeni",
            Screening => "Ön Eleme",
            Interview => "Mülakat",
            Withdrawn => "Geri Çekildi",
            _ => status
        };
    }
}
