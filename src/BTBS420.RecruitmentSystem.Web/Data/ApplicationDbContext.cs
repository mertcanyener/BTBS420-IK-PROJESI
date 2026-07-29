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

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<JobFamily> JobFamilies => Set<JobFamily>();

    public DbSet<Seniority> Seniorities => Set<Seniority>();

    public DbSet<Position> Positions => Set<Position>();

    public DbSet<Skill> Skills => Set<Skill>();

    public DbSet<Education> Educations => Set<Education>();

    public DbSet<Language> Languages => Set<Language>();

    public DbSet<Location> Locations => Set<Location>();

    public DbSet<ExperienceRange> ExperienceRanges => Set<ExperienceRange>();

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

        builder.Entity<ApplicationUser>()
            .HasIndex(user => user.NormalizedEmail)
            .HasDatabaseName("EmailIndex")
            .IsUnique()
            .HasFilter("[NormalizedEmail] IS NOT NULL");

        builder.ApplyConfiguration(new ActivityLogConfiguration());
        builder.ApplyConfiguration(new NotificationConfiguration());
        builder.ApplyConfiguration(new DepartmentConfiguration());
        builder.ApplyConfiguration(new JobFamilyConfiguration());
        builder.ApplyConfiguration(new SeniorityConfiguration());
        builder.ApplyConfiguration(new PositionConfiguration());
        builder.ApplyConfiguration(new SkillConfiguration());
        builder.ApplyConfiguration(new EducationConfiguration());
        builder.ApplyConfiguration(new LanguageConfiguration());
        builder.ApplyConfiguration(new LocationConfiguration());
        builder.ApplyConfiguration(new ExperienceRangeConfiguration());
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
