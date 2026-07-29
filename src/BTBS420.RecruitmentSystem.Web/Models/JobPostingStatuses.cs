using System.Collections.Frozen;

namespace BTBS420.RecruitmentSystem.Web.Models;

public static class JobPostingStatuses
{
    public const string Draft = "draft";
    public const string Published = "published";
    public const string ApplicationsClosed = "applications-closed";
    public const string PositionFilled = "position-filled";

    private static readonly FrozenSet<string> DefinedStatuses =
        new[] { Draft, Published, ApplicationsClosed, PositionFilled }
            .ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, FrozenSet<string>> AllowedTransitions =
        new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal)
        {
            [Draft] = new[] { Published }.ToFrozenSet(StringComparer.Ordinal),
            [Published] = new[] { ApplicationsClosed, PositionFilled }
                .ToFrozenSet(StringComparer.Ordinal),
            [ApplicationsClosed] = new[] { Published, PositionFilled }
                .ToFrozenSet(StringComparer.Ordinal),
            [PositionFilled] = FrozenSet<string>.Empty
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => DefinedStatuses;

    public static bool IsDefined(string status)
    {
        return DefinedStatuses.Contains(status);
    }

    public static bool IsValidTransition(string currentStatus, string newStatus)
    {
        return AllowedTransitions.TryGetValue(currentStatus, out var allowedNextStatuses) &&
            allowedNextStatuses.Contains(newStatus);
    }

    public static IReadOnlySet<string> GetAllowedNextStatuses(string currentStatus)
    {
        return AllowedTransitions.TryGetValue(currentStatus, out var allowedNextStatuses)
            ? allowedNextStatuses
            : FrozenSet<string>.Empty;
    }
}
