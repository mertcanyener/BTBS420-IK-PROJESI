using System.Collections.Frozen;

namespace BTBS420.RecruitmentSystem.Web.Models;

public static class InterviewStatuses
{
    public const string Scheduled = "scheduled";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";

    private static readonly FrozenSet<string> DefinedStatuses =
        new[] { Scheduled, Completed, Cancelled }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => DefinedStatuses;

    public static bool IsDefined(string status)
    {
        return DefinedStatuses.Contains(status);
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
