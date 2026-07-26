using BTBS420.RecruitmentSystem.Web.Data.Configurations;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Data;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public override int SaveChanges()
    {
        return SaveChanges(acceptAllChangesOnSuccess: true);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureActivityLogsAreAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(
            acceptAllChangesOnSuccess: true,
            cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureActivityLogsAreAppendOnly();
        return base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>()
            .Property(user => user.IsActive)
            .HasDefaultValue(true);

        builder.ApplyConfiguration(new ActivityLogConfiguration());
        builder.ApplyConfiguration(new NotificationConfiguration());
    }

    private void EnsureActivityLogsAreAppendOnly()
    {
        var mutatedActivityLog = ChangeTracker
            .Entries<ActivityLog>()
            .FirstOrDefault(
                entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (mutatedActivityLog is not null)
        {
            throw new InvalidOperationException(
                "Aktivite kayıtları yalnızca eklenebilir; güncellenemez veya silinemez.");
        }
    }
}
