namespace BTBS420.RecruitmentSystem.Web.Notifications;

public interface INotificationPublisher
{
    /// <summary>
    /// Aynı alıcı ve olay anahtarı için bir bildirim yoksa bildirimi mevcut
    /// ApplicationDbContext örneğinde eklenmiş olarak işaretler.
    /// Kaydetme veya transaction commit işlemi yapmaz; çağıran aynı
    /// SaveChanges/transaction kapsamında kalıcılığı sağlamalıdır.
    /// </summary>
    /// <returns>Yeni bildirim eklendiyse true; zaten mevcutsa false.</returns>
    Task<bool> StageIfMissingAsync(
        NotificationEntry entry,
        CancellationToken cancellationToken = default);
}
