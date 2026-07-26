using System.Collections.Frozen;

namespace BTBS420.RecruitmentSystem.Web.ActivityLogging;

public static class ActivityActionCodes
{
    public const string AuthenticationSucceeded = "authentication.succeeded";
    public const string AuthenticationFailed = "authentication.failed";
    public const string AuthenticationSignedOut = "authentication.signed-out";
    public const string AuthorizationDenied = "authorization.denied";
    public const string EntityCreated = "entity.created";
    public const string EntityUpdated = "entity.updated";
    public const string EntityStatusChanged = "entity.status-changed";
    public const string EntityArchived = "entity.archived";
    public const string EntityDeleted = "entity.deleted";

    private static readonly FrozenSet<string> DefinedCodes =
        new[]
        {
            AuthenticationSucceeded,
            AuthenticationFailed,
            AuthenticationSignedOut,
            AuthorizationDenied,
            EntityCreated,
            EntityUpdated,
            EntityStatusChanged,
            EntityArchived,
            EntityDeleted
        }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => DefinedCodes;

    public static bool IsDefined(string actionCode)
    {
        return DefinedCodes.Contains(actionCode);
    }
}
