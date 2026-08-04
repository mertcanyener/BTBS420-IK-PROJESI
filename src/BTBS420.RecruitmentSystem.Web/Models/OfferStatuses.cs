using System.Collections.Frozen;

namespace BTBS420.RecruitmentSystem.Web.Models;

public static class OfferStatuses
{
    public const string Draft = "draft";
    public const string PendingManagerApproval = "pending_manager_approval";
    public const string Approved = "approved";
    public const string RejectedByManager = "rejected_by_manager";

    private static readonly FrozenSet<string> DefinedStatuses =
        new[] { Draft, PendingManagerApproval, Approved, RejectedByManager }
            .ToFrozenSet(StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, FrozenSet<string>> AllowedTransitions =
        new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal)
        {
            [Draft] = new[] { PendingManagerApproval }.ToFrozenSet(StringComparer.Ordinal),
            [PendingManagerApproval] =
                new[] { Approved, RejectedByManager }.ToFrozenSet(StringComparer.Ordinal),
            [Approved] = FrozenSet<string>.Empty,
            [RejectedByManager] = FrozenSet<string>.Empty
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
            Draft => "Taslak",
            PendingManagerApproval => "Yönetici Onayı Bekliyor",
            Approved => "Onaylandı",
            RejectedByManager => "Yönetici Tarafından Reddedildi",
            _ => status
        };
    }
}
