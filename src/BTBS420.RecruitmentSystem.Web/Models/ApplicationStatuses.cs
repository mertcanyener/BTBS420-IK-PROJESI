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

    private static readonly FrozenSet<string> WithdrawableStatuses =
        new[] { New, Screening, Interview }
            .ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => DefinedStatuses;

    public static bool IsDefined(string status)
    {
        return DefinedStatuses.Contains(status);
    }

    public static bool CanWithdraw(string status)
    {
        return WithdrawableStatuses.Contains(status);
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
