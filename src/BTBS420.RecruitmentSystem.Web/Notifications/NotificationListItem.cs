namespace BTBS420.RecruitmentSystem.Web.Notifications;

public sealed record NotificationListItem(
    long Id,
    string Title,
    string Message,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc)
{
    public bool IsRead => ReadAtUtc.HasValue;
}
