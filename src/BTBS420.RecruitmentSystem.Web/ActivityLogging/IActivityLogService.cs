using BTBS420.RecruitmentSystem.Web.Models;

namespace BTBS420.RecruitmentSystem.Web.ActivityLogging;

public interface IActivityLogService
{
    /// <summary>
    /// Aktivite kaydını mevcut <see cref="Data.ApplicationDbContext"/> örneğinde
    /// eklenmiş olarak işaretler; kaydetme veya transaction commit işlemi yapmaz.
    /// Çağıran, domain değişiklikleriyle aynı SaveChanges veya transaction
    /// kapsamında kalıcılığı sağlamalıdır.
    /// </summary>
    ActivityLog Stage(ActivityLogEntry entry);
}
