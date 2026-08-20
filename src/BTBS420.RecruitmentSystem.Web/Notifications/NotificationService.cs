using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Notifications;

public sealed class NotificationService(
    ApplicationDbContext dbContext,
    ICurrentActorAccessor currentActorAccessor,
    TimeProvider timeProvider) : INotificationPublisher, INotificationCenterService
{
    public async Task<bool> StageIfMissingAsync(
        NotificationEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var notification = new Notification(
            entry.RecipientUserId,
            entry.EventKey,
            entry.Title,
            entry.Message,
            timeProvider.GetUtcNow());

        var isAlreadyTracked = dbContext.ChangeTracker
            .Entries<Notification>()
            .Any(
                trackedEntry =>
                    trackedEntry.State is not EntityState.Deleted &&
                    string.Equals(
                        trackedEntry.Entity.RecipientUserId,
                        notification.RecipientUserId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        trackedEntry.Entity.EventKey,
                        notification.EventKey,
                        StringComparison.Ordinal));

        if (isAlreadyTracked)
        {
            return false;
        }

        var alreadyExists = await dbContext.Notifications
            .AsNoTracking()
            .AnyAsync(
                candidate =>
                    candidate.RecipientUserId == notification.RecipientUserId &&
                    candidate.EventKey == notification.EventKey,
                cancellationToken);

        if (alreadyExists)
        {
            return false;
        }

        dbContext.Notifications.Add(notification);
        return true;
    }

    public async Task<IReadOnlyList<NotificationListItem>> GetNotificationsAsync(
        CancellationToken cancellationToken = default)
    {
        var recipientUserId = GetCurrentRecipientUserId();

        return await dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.RecipientUserId == recipientUserId)
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .ThenByDescending(notification => notification.Id)
            .Select(
                notification => new NotificationListItem(
                    notification.Id,
                    notification.Title,
                    notification.Message,
                    notification.CreatedAtUtc,
                    notification.ReadAtUtc))
            .ToListAsync(cancellationToken);
    }

    public Task<int> GetUnreadCountAsync(
        CancellationToken cancellationToken = default)
    {
        var recipientUserId = GetCurrentRecipientUserId();

        return dbContext.Notifications.CountAsync(
            notification =>
                notification.RecipientUserId == recipientUserId &&
                notification.ReadAtUtc == null,
            cancellationToken);
    }

    public async Task<bool> MarkAsReadAsync(
        long notificationId,
        CancellationToken cancellationToken = default)
    {
        if (notificationId <= 0)
        {
            return false;
        }

        var recipientUserId = GetCurrentRecipientUserId();
        var readAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
        var updatedCount = await dbContext.Notifications
            .Where(
                candidate =>
                    candidate.Id == notificationId &&
                    candidate.RecipientUserId == recipientUserId &&
                    candidate.ReadAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    candidate => candidate.ReadAtUtc,
                    readAtUtc),
                cancellationToken);

        if (updatedCount > 0)
        {
            return true;
        }

        return await dbContext.Notifications
            .AsNoTracking()
            .AnyAsync(
                candidate =>
                    candidate.Id == notificationId &&
                    candidate.RecipientUserId == recipientUserId,
                cancellationToken);
    }

    public async Task<int> MarkAllAsReadAsync(
        CancellationToken cancellationToken = default)
    {
        var recipientUserId = GetCurrentRecipientUserId();
        var readAtUtc = timeProvider.GetUtcNow().ToUniversalTime();

        return await dbContext.Notifications
            .Where(
                notification =>
                    notification.RecipientUserId == recipientUserId &&
                    notification.ReadAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    notification => notification.ReadAtUtc,
                    readAtUtc),
                cancellationToken);
    }

    private string GetCurrentRecipientUserId()
    {
        var recipientUserId = currentActorAccessor.GetUserId();

        if (string.IsNullOrWhiteSpace(recipientUserId))
        {
            throw new UnauthorizedAccessException(
                "Bildirim merkezi için kimliği doğrulanmış kullanıcı gereklidir.");
        }

        return Notification.NormalizeRecipientUserId(recipientUserId);
    }
}
