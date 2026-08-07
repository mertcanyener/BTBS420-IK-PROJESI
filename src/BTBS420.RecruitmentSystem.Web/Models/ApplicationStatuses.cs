using System.Collections.Frozen;

namespace BTBS420.RecruitmentSystem.Web.Models;

public static class ApplicationStatuses
{
    public const string New = "new";
    public const string Screening = "screening";
    public const string Interview = "interview";
    public const string Withdrawn = "withdrawn";
    public const string Rejected = "rejected";
    public const string Hired = "hired";

    private static readonly FrozenSet<string> DefinedStatuses =
        new[] { New, Screening, Interview, Withdrawn, Rejected, Hired }
            .ToFrozenSet(StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, FrozenSet<string>> AllowedTransitions =
        new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal)
        {
            [New] = new[] { Screening, Withdrawn }.ToFrozenSet(StringComparer.Ordinal),
            [Screening] = new[] { Interview, Withdrawn, Rejected }.ToFrozenSet(StringComparer.Ordinal),
            [Interview] = new[] { Withdrawn, Rejected, Hired }.ToFrozenSet(StringComparer.Ordinal),
            [Rejected] = new[] { Screening }.ToFrozenSet(StringComparer.Ordinal),
            [Withdrawn] = FrozenSet<string>.Empty,
            [Hired] = FrozenSet<string>.Empty
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
            Rejected => "Reddedildi",
            Hired => "İşe Alındı",
            _ => status
        };
    }
}
