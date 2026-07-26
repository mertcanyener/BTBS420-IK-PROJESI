using BTBS420.RecruitmentSystem.Web.Notifications;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.Notifications;

public sealed class NotificationIndexViewModel(
    IReadOnlyList<NotificationListItem> notifications)
{
    public IReadOnlyList<NotificationListItem> Notifications { get; } =
        notifications ?? throw new ArgumentNullException(nameof(notifications));

    public int UnreadCount => Notifications.Count(notification => !notification.IsRead);
}
