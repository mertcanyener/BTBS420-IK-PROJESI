using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.Notifications;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class NotificationSqlServerIntegrationTests
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN30_TEST_SQLSERVER_CONNECTION_STRING";

    [SqlServerIntegrationFact]
    public async Task Liste_YalnizCurrentUserKayitlariniEnYenidenEskiyeDondurur()
    {
        await using var context = CreateContext();
        await AssertNotificationMigrationAppliedAsync(context);
        var runId = Guid.NewGuid().ToString("N");
        var currentUserId = $"kan30-list-current-{runId}";
        var otherUserId = $"kan30-list-other-{runId}";
        await AddUsersAsync(context, currentUserId, otherUserId);
        var olderUtc = new DateTimeOffset(
            2026,
            7,
            25,
            8,
            0,
            0,
            TimeSpan.Zero);
        var newestUtc = olderUtc.AddHours(1);

        var olderId = await StageAndSaveAsync(
            context,
            currentUserId,
            "notification:test:list-older",
            olderUtc);
        var firstNewestId = await StageAndSaveAsync(
            context,
            currentUserId,
            "notification:test:list-newest-a",
            newestUtc);
        var secondNewestId = await StageAndSaveAsync(
            context,
            currentUserId,
            "notification:test:list-newest-b",
            newestUtc);
        var otherUserNotificationId = await StageAndSaveAsync(
            context,
            otherUserId,
            "notification:test:list-other",
            newestUtc.AddMinutes(1));
        var center = CreateService(
            context,
            currentUserId,
            newestUtc.AddHours(1));

        var notifications = await center.GetNotificationsAsync();

        Assert.Equal(
            [secondNewestId, firstNewestId, olderId],
            notifications.Select(notification => notification.Id));
        Assert.DoesNotContain(
            notifications,
            notification => notification.Id == otherUserNotificationId);
    }

    [SqlServerIntegrationFact]
    public async Task UnreadCountVeMarkAsRead_BaskaKullanicininKaydiniDegistirmez()
    {
        await using var context = CreateContext();
        await AssertNotificationMigrationAppliedAsync(context);
        var runId = Guid.NewGuid().ToString("N");
        var currentUserId = $"kan30-read-current-{runId}";
        var otherUserId = $"kan30-read-other-{runId}";
        await AddUsersAsync(context, currentUserId, otherUserId);
        var createdAtUtc = new DateTimeOffset(
            2026,
            7,
            25,
            9,
            0,
            0,
            TimeSpan.Zero);
        var firstOwnId = await StageAndSaveAsync(
            context,
            currentUserId,
            "notification:test:read-first",
            createdAtUtc);
        await StageAndSaveAsync(
            context,
            currentUserId,
            "notification:test:read-second",
            createdAtUtc.AddMinutes(1));
        var otherUserNotificationId = await StageAndSaveAsync(
            context,
            otherUserId,
            "notification:test:read-foreign",
            createdAtUtc);
        var readAtUtc = createdAtUtc.AddHours(1);
        var center = CreateService(context, currentUserId, readAtUtc);

        Assert.Equal(2, await center.GetUnreadCountAsync());
        Assert.False(await center.MarkAsReadAsync(otherUserNotificationId));
        Assert.False(await center.MarkAsReadAsync(long.MaxValue));
        Assert.Equal(2, await center.GetUnreadCountAsync());
        Assert.True(await center.MarkAsReadAsync(firstOwnId));
        Assert.Equal(1, await center.GetUnreadCountAsync());
        var laterCenter = CreateService(
            context,
            currentUserId,
            readAtUtc.AddHours(1));
        Assert.True(await laterCenter.MarkAsReadAsync(firstOwnId));

        context.ChangeTracker.Clear();
        var ownNotification = await context.Notifications
            .AsNoTracking()
            .SingleAsync(notification => notification.Id == firstOwnId);
        var foreignNotification = await context.Notifications
            .AsNoTracking()
            .SingleAsync(
                notification => notification.Id == otherUserNotificationId);

        Assert.Equal(readAtUtc, ownNotification.ReadAtUtc);
        Assert.Null(foreignNotification.ReadAtUtc);
    }

    [SqlServerIntegrationFact]
    public async Task Publisher_SaveChangesYapmadanTekKaydiStageEder()
    {
        await using var context = CreateContext();
        await AssertNotificationMigrationAppliedAsync(context);
        var runId = Guid.NewGuid().ToString("N");
        var recipientId = $"kan30-stage-{runId}";
        await AddUsersAsync(context, recipientId);
        context.ChangeTracker.Clear();
        var eventKey = $"notification:test:stage-{runId}";
        var publisher = CreateService(
            context,
            recipientId,
            new DateTimeOffset(
                2026,
                7,
                25,
                9,
                30,
                0,
                TimeSpan.Zero));
        var entry = new NotificationEntry(
            recipientId,
            eventKey,
            "Bildirim",
            "Henüz kaydedilmemiş bildirim.");

        Assert.True(await publisher.StageIfMissingAsync(entry));
        Assert.False(await publisher.StageIfMissingAsync(entry));
        Assert.Single(
            context.ChangeTracker.Entries<Notification>(),
            entryState => entryState.State == EntityState.Added);

        await using (var verificationContext = CreateContext())
        {
            Assert.False(
                await verificationContext.Notifications
                    .AsNoTracking()
                    .AnyAsync(
                        notification =>
                            notification.RecipientUserId == recipientId &&
                            notification.EventKey == eventKey));
        }

        await context.SaveChangesAsync();

        await using var persistedContext = CreateContext();
        Assert.Equal(
            1,
            await persistedContext.Notifications.CountAsync(
                notification =>
                    notification.RecipientUserId == recipientId &&
                    notification.EventKey == eventKey));
    }

    [SqlServerIntegrationFact]
    public async Task MarkAllAsRead_YalnizCurrentUserBildirimleriniGunceller()
    {
        await using var context = CreateContext();
        await AssertNotificationMigrationAppliedAsync(context);
        var runId = Guid.NewGuid().ToString("N");
        var currentUserId = $"kan30-all-current-{runId}";
        var otherUserId = $"kan30-all-other-{runId}";
        await AddUsersAsync(context, currentUserId, otherUserId);
        var createdAtUtc = new DateTimeOffset(
            2026,
            7,
            25,
            10,
            0,
            0,
            TimeSpan.Zero);
        await StageAndSaveAsync(
            context,
            currentUserId,
            "notification:test:all-first",
            createdAtUtc);
        await StageAndSaveAsync(
            context,
            currentUserId,
            "notification:test:all-second",
            createdAtUtc.AddMinutes(1));
        var otherUserNotificationId = await StageAndSaveAsync(
            context,
            otherUserId,
            "notification:test:all-foreign",
            createdAtUtc);
        var readAtUtc = createdAtUtc.AddHours(2);
        var center = CreateService(context, currentUserId, readAtUtc);

        var markedCount = await center.MarkAllAsReadAsync();

        Assert.Equal(2, markedCount);
        Assert.Equal(0, await center.GetUnreadCountAsync());
        context.ChangeTracker.Clear();
        Assert.All(
            await context.Notifications
                .AsNoTracking()
                .Where(
                    notification =>
                        notification.RecipientUserId == currentUserId)
                .ToListAsync(),
            notification => Assert.Equal(readAtUtc, notification.ReadAtUtc));
        Assert.Null(
            (await context.Notifications
                .AsNoTracking()
                .SingleAsync(
                    notification =>
                        notification.Id == otherUserNotificationId))
            .ReadAtUtc);
    }

    [SqlServerIntegrationFact]
    public async Task Publisher_AyniRecipientEventiTekKayitTutarFarkliRecipientiAyirir()
    {
        await using var context = CreateContext();
        await AssertNotificationMigrationAppliedAsync(context);
        var runId = Guid.NewGuid().ToString("N");
        var firstRecipientId = $"kan30-dedupe-first-{runId}";
        var secondRecipientId = $"kan30-dedupe-second-{runId}";
        await AddUsersAsync(context, firstRecipientId, secondRecipientId);
        var createdAtUtc = new DateTimeOffset(
            2026,
            7,
            25,
            11,
            0,
            0,
            TimeSpan.Zero);
        var publisher = CreateService(
            context,
            firstRecipientId,
            createdAtUtc);
        var uppercaseEntry = new NotificationEntry(
            firstRecipientId,
            " NOTIFICATION:TEST:KAN30 ",
            "Başvuru durumu",
            "Başvurunuz güncellendi.");

        Assert.True(await publisher.StageIfMissingAsync(uppercaseEntry));
        await context.SaveChangesAsync();
        Assert.False(
            await publisher.StageIfMissingAsync(
                uppercaseEntry with
                {
                    EventKey = "notification:test:kan30"
                }));
        Assert.True(
            await publisher.StageIfMissingAsync(
                uppercaseEntry with
                {
                    RecipientUserId = secondRecipientId,
                    EventKey = "notification:test:kan30"
                }));
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        var persisted = await context.Notifications
            .AsNoTracking()
            .Where(
                notification =>
                    notification.EventKey == "notification:test:kan30" &&
                    (notification.RecipientUserId == firstRecipientId ||
                     notification.RecipientUserId == secondRecipientId))
            .OrderBy(notification => notification.RecipientUserId)
            .ToListAsync();

        Assert.Equal(2, persisted.Count);
        Assert.Equal(
            [firstRecipientId, secondRecipientId],
            persisted
                .Select(notification => notification.RecipientUserId)
                .Order(StringComparer.Ordinal));
        Assert.All(
            persisted,
            notification =>
                Assert.Equal(
                    "notification:test:kan30",
                    notification.EventKey));
    }

    [SqlServerIntegrationFact]
    public async Task UniqueIndex_DogrudanIkinciRecipientEventKaydiniReddeder()
    {
        await using var context = CreateContext();
        await AssertNotificationMigrationAppliedAsync(context);
        var runId = Guid.NewGuid().ToString("N");
        var recipientId = $"kan30-unique-{runId}";
        await AddUsersAsync(context, recipientId);
        var createdAtUtc = new DateTimeOffset(
            2026,
            7,
            25,
            12,
            0,
            0,
            TimeSpan.Zero);
        await StageAndSaveAsync(
            context,
            recipientId,
            "notification:test:unique",
            createdAtUtc);

        await Assert.ThrowsAnyAsync<Exception>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO [Notifications]
                     ([RecipientUserId], [EventKey], [Title], [Message], [CreatedAtUtc], [ReadAtUtc])
                 VALUES
                     ({recipientId}, {"notification:test:unique"}, {"Bildirim"}, {"Mükerrer"}, {createdAtUtc}, {(DateTimeOffset?)null})
                 """));

        Assert.Equal(
            1,
            await context.Notifications.CountAsync(
                notification =>
                    notification.RecipientUserId == recipientId &&
                    notification.EventKey == "notification:test:unique"));
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

    private static async Task AssertNotificationMigrationAppliedAsync(
        ApplicationDbContext context)
    {
        Assert.Contains(
            await context.Database.GetAppliedMigrationsAsync(),
            migration => migration.EndsWith(
                "_AddNotificationInfrastructure",
                StringComparison.Ordinal));
    }

    private static async Task AddUsersAsync(
        ApplicationDbContext context,
        params string[] userIds)
    {
        foreach (var userId in userIds)
        {
            context.Users.Add(
                new ApplicationUser
                {
                    Id = userId,
                    UserName = userId,
                    NormalizedUserName = userId.ToUpperInvariant()
                });
        }

        await context.SaveChangesAsync();
    }

    private static async Task<long> StageAndSaveAsync(
        ApplicationDbContext context,
        string recipientUserId,
        string eventKey,
        DateTimeOffset createdAtUtc)
    {
        var service = CreateService(
            context,
            recipientUserId,
            createdAtUtc);
        var wasStaged = await service.StageIfMissingAsync(
            new NotificationEntry(
                recipientUserId,
                eventKey,
                $"Başlık {eventKey}",
                $"Mesaj {eventKey}"));

        Assert.True(wasStaged);
        await context.SaveChangesAsync();

        return await context.Notifications
            .Where(
                notification =>
                    notification.RecipientUserId == recipientUserId &&
                    notification.EventKey == eventKey)
            .Select(notification => notification.Id)
            .SingleAsync();
    }

    private static NotificationService CreateService(
        ApplicationDbContext context,
        string? actorUserId,
        DateTimeOffset utcNow)
    {
        return new NotificationService(
            context,
            new StubCurrentActorAccessor(actorUserId),
            new FixedTimeProvider(utcNow));
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
                    "geçici SQL Server bildirim entegrasyon testi atlandı.";
            }
        }
    }
}
