using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class ActivityLogTransactionTests
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN23_TEST_SQLSERVER_CONNECTION_STRING";

    [SqlServerIntegrationFact]
    public async Task DomainVeAudit_AyniSqlServerTransactionindaBirlikteCommitOlur()
    {
        await using var context = CreateContext();
        await AssertActivityLogMigrationAppliedAsync(context);

        var testRunId = Guid.NewGuid().ToString("N");
        var committedUserId = $"kan23-committed-{testRunId}";
        var service = CreateService(context);

        await using (var transaction =
                     await context.Database.BeginTransactionAsync())
        {
            context.Users.Add(
                CreateUser(committedUserId));
            service.Stage(
                new ActivityLogEntry(
                    ActivityActionCodes.EntityCreated,
                    "Kullanıcı oluşturuldu password=KAN23-TransactionSecret",
                    ActivityEntityTypes.User,
                    committedUserId));

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        context.ChangeTracker.Clear();

        Assert.True(
            await context.Users.AnyAsync(
                user => user.Id == committedUserId));
        var committedLog = await context.ActivityLogs.SingleAsync(
            activityLog => activityLog.TargetEntityId == committedUserId);
        Assert.Equal(committedUserId, committedLog.TargetEntityId);
        Assert.Contains(ActivityLogRedactor.RedactedValue, committedLog.Summary);
        Assert.DoesNotContain("KAN23-TransactionSecret", committedLog.Summary);
    }

    [SqlServerIntegrationFact]
    public async Task RollbackEdilenDomainIslemindeBasariAuditKaydiKalmaz()
    {
        await using var context = CreateContext();
        await AssertActivityLogMigrationAppliedAsync(context);

        var testRunId = Guid.NewGuid().ToString("N");
        var rolledBackUserId = $"kan23-rolled-back-{testRunId}";
        var service = CreateService(context);

        await using (var transaction =
                     await context.Database.BeginTransactionAsync())
        {
            context.Users.Add(
                CreateUser(rolledBackUserId));
            service.Stage(
                new ActivityLogEntry(
                    ActivityActionCodes.EntityCreated,
                    "Rollback kullanıcı kaydı.",
                    ActivityEntityTypes.User,
                    rolledBackUserId));

            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        context.ChangeTracker.Clear();

        Assert.False(
            await context.Users.AnyAsync(
                user => user.Id == rolledBackUserId));
        Assert.False(
            await context.ActivityLogs.AnyAsync(
                activityLog =>
                    activityLog.TargetEntityId == rolledBackUserId));
    }

    [SqlServerIntegrationFact]
    public async Task AppendOnlyTrigger_InserteIzinVerirUpdateVeDeleteIslemleriniReddeder()
    {
        await using var context = CreateContext();
        await AssertActivityLogMigrationAppliedAsync(context);

        var targetId = $"kan23-trigger-{Guid.NewGuid():N}";
        var service = CreateService(context);
        var activityLog = service.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityCreated,
                "Trigger doğrulama kaydı.",
                ActivityEntityTypes.System,
                targetId));

        await context.SaveChangesAsync();

        Assert.True(activityLog.Id > 0);
        context.ChangeTracker.Clear();

        var updateException = await Assert.ThrowsAnyAsync<Exception>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 UPDATE ActivityLogs
                 SET Summary = N'Değiştirildi'
                 WHERE Id = {activityLog.Id}
                 """));

        Assert.Contains(
            "append-only",
            updateException.Message,
            StringComparison.OrdinalIgnoreCase);

        var deleteException = await Assert.ThrowsAnyAsync<Exception>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 DELETE FROM ActivityLogs
                 WHERE Id = {activityLog.Id}
                 """));

        Assert.Contains(
            "append-only",
            deleteException.Message,
            StringComparison.OrdinalIgnoreCase);

        var persistedLog = await context.ActivityLogs.SingleAsync(
            log => log.Id == activityLog.Id);
        Assert.Equal("Trigger doğrulama kaydı.", persistedLog.Summary);
    }

    private static ApplicationDbContext CreateContext()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)!;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task AssertActivityLogMigrationAppliedAsync(
        ApplicationDbContext context)
    {
        Assert.Contains(
            await context.Database.GetAppliedMigrationsAsync(),
            migration => migration.EndsWith(
                "_AddActivityLogInfrastructure",
                StringComparison.Ordinal));
    }

    private static ApplicationUser CreateUser(string userId)
    {
        return new ApplicationUser
        {
            Id = userId,
            UserName = userId,
            NormalizedUserName = userId.ToUpperInvariant()
        };
    }

    private static ActivityLogService CreateService(
        ApplicationDbContext context)
    {
        return new ActivityLogService(
            context,
            new StubCurrentActorAccessor("kan23-transaction-actor"),
            new ActivityLogRedactor(),
            new FixedTimeProvider(
                new DateTimeOffset(
                    2026,
                    7,
                    24,
                    11,
                    0,
                    0,
                    TimeSpan.Zero)));
    }

    private sealed class StubCurrentActorAccessor(string? userId)
        : ICurrentActorAccessor
    {
        public string? GetUserId()
        {
            return userId;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }

    private sealed class SqlServerIntegrationFactAttribute : FactAttribute
    {
        public SqlServerIntegrationFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable(
                        ConnectionStringEnvironmentVariable)))
            {
                Skip =
                    $"{ConnectionStringEnvironmentVariable} ayarlanmadığı için " +
                    "geçici SQL Server entegrasyon testi atlandı.";
            }
        }
    }
}
