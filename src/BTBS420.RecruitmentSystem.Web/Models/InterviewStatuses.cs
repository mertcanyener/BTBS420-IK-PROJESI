using System.Collections.Frozen;

namespace BTBS420.RecruitmentSystem.Web.Models;

public static class InterviewStatuses
{
    public const string Scheduled = "scheduled";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";

    private static readonly FrozenSet<string> DefinedStatuses =
        new[] { Scheduled, Completed, Cancelled }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, FrozenSet<string>> AllowedTransitions =
        new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal)
        {
            [Scheduled] = new[] { Completed, Cancelled }.ToFrozenSet(StringComparer.Ordinal),
            [Completed] = FrozenSet<string>.Empty,
            [Cancelled] = FrozenSet<string>.Empty
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

    public static string GetDisplayLabel(string status)
    {
        return status switch
        {
            Scheduled => "Planlandı",
            Completed => "Tamamlandı",
            Cancelled => "İptal Edildi",
            _ => status
        };
    }
}
