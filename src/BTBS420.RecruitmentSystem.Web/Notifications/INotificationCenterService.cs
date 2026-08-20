namespace BTBS420.RecruitmentSystem.Web.Notifications;

public interface INotificationCenterService
{
    Task<IReadOnlyList<NotificationListItem>> GetNotificationsAsync(
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        CancellationToken cancellationToken = default);

    Task<bool> MarkAsReadAsync(
        long notificationId,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllAsReadAsync(
        CancellationToken cancellationToken = default);
}
