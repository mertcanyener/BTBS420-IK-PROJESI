using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Data.Configurations;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class NotificationInfrastructureTests :
    IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public NotificationInfrastructureTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void ApplicationDbContext_NotificationModeliniIliskiVeIndekslerleIcerir()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var designTimeModel = context.GetService<IDesignTimeModel>().Model;
        var entityType = designTimeModel.FindEntityType(typeof(Notification));

        Assert.NotNull(entityType);
        Assert.Equal(NotificationConfiguration.TableName, entityType.GetTableName());
        Assert.Equal(
            Notification.MaximumRecipientUserIdLength,
            entityType.FindProperty(nameof(Notification.RecipientUserId))?.GetMaxLength());
        Assert.Equal(
            Notification.MaximumEventKeyLength,
            entityType.FindProperty(nameof(Notification.EventKey))?.GetMaxLength());
        Assert.False(
            entityType.FindProperty(nameof(Notification.EventKey))?.IsUnicode());
        Assert.Equal(
            Notification.MaximumTitleLength,
            entityType.FindProperty(nameof(Notification.Title))?.GetMaxLength());
        Assert.Equal(
            Notification.MaximumMessageLength,
            entityType.FindProperty(nameof(Notification.Message))?.GetMaxLength());
        Assert.Equal(
            "datetimeoffset(7)",
            entityType.FindProperty(nameof(Notification.CreatedAtUtc))?.GetColumnType());
        Assert.Equal(
            "datetimeoffset(7)",
            entityType.FindProperty(nameof(Notification.ReadAtUtc))?.GetColumnType());
        Assert.Null(entityType.FindProperty(nameof(Notification.IsRead)));

        var recipientForeignKey = Assert.Single(entityType.GetForeignKeys());
        Assert.Equal(
            typeof(ApplicationUser),
            recipientForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(
            [nameof(Notification.RecipientUserId)],
            recipientForeignKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Cascade, recipientForeignKey.DeleteBehavior);

        var indexes = entityType.GetIndexes().ToDictionary(
            index => index.GetDatabaseName()!,
            StringComparer.Ordinal);

        var uniqueIndex =
            indexes["UX_Notifications_RecipientUserId_EventKey"];
        Assert.True(uniqueIndex.IsUnique);
        Assert.Equal(
            [nameof(Notification.RecipientUserId), nameof(Notification.EventKey)],
            uniqueIndex.Properties.Select(property => property.Name));

        var listIndex =
            indexes["IX_Notifications_RecipientUserId_CreatedAtUtc_Id"];
        Assert.Equal(
            [
                nameof(Notification.RecipientUserId),
                nameof(Notification.CreatedAtUtc),
                nameof(Notification.Id)
            ],
            listIndex.Properties.Select(property => property.Name));
        Assert.Equal([false, true, true], listIndex.IsDescending);

        var unreadIndex =
            indexes["IX_Notifications_RecipientUserId_Unread"];
        Assert.Equal(
            [nameof(Notification.RecipientUserId)],
            unreadIndex.Properties.Select(property => property.Name));
        Assert.Equal("[ReadAtUtc] IS NULL", unreadIndex.GetFilter());
    }

    [Fact]
    public void NotificationServisi_ScopedOlarakKaydedilirVeSozlesmelerCozumlenir()
    {
        using var firstScope = _factory.Services.CreateScope();
        using var secondScope = _factory.Services.CreateScope();

        var firstService =
            firstScope.ServiceProvider.GetRequiredService<NotificationService>();
        var sameScopeService =
            firstScope.ServiceProvider.GetRequiredService<NotificationService>();
        var secondService =
            secondScope.ServiceProvider.GetRequiredService<NotificationService>();

        Assert.Same(firstService, sameScopeService);
        Assert.NotSame(firstService, secondService);
        Assert.NotNull(
            firstScope.ServiceProvider.GetRequiredService<INotificationPublisher>());
        Assert.NotNull(
            firstScope.ServiceProvider.GetRequiredService<INotificationCenterService>());
    }

    [Fact]
    public void ApplicationDbContext_AddNotificationInfrastructureMigrationiniIcerir()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Contains(
            context.Database.GetMigrations(),
            migration => migration.EndsWith(
                "_AddNotificationInfrastructure",
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("-application:42")]
    [InlineData("application:42-")]
    [InlineData("application status:42")]
    [InlineData("application:\r\n42")]
    [InlineData("application:\u202E42")]
    [InlineData("application:password:42")]
    [InlineData("application:token:42")]
    [InlineData("application:secret:42")]
    [InlineData("application:email:42")]
    [InlineData("application:content:42")]
    public async Task Publisher_GuvenliOlmayanEventKeyiVeritabaniBaglantisiAcmadanReddeder(
        string eventKey)
    {
        await using var context = CreateDisconnectedContext();
        var service = CreateService(context);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.StageIfMissingAsync(
                new NotificationEntry(
                    "candidate-30",
                    eventKey,
                    "Başlık",
                    "Güvenli bildirim metni.")));

        Assert.Empty(context.ChangeTracker.Entries<Notification>());
        Assert.Equal(
            System.Data.ConnectionState.Closed,
            context.Database.GetDbConnection().State);
    }

    public static TheoryData<NotificationEntry> InvalidEntries =>
        new()
        {
            new NotificationEntry(
                " ",
                "application:status:42",
                "Başlık",
                "Mesaj"),
            new NotificationEntry(
                "candidate-30",
                "application:status:42",
                " ",
                "Mesaj"),
            new NotificationEntry(
                "candidate-30",
                "application:status:42",
                new string('B', Notification.MaximumTitleLength + 1),
                "Mesaj"),
            new NotificationEntry(
                "candidate-30",
                "application:status:42",
                "Başlık",
                new string('M', Notification.MaximumMessageLength + 1))
        };

    [Theory]
    [MemberData(nameof(InvalidEntries))]
    public async Task Publisher_GecersizAliciBaslikVeyaMesajiReddeder(
        NotificationEntry entry)
    {
        await using var context = CreateDisconnectedContext();
        var service = CreateService(context);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.StageIfMissingAsync(entry));

        Assert.Empty(context.ChangeTracker.Entries<Notification>());
    }

    private static ApplicationDbContext CreateDisconnectedContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(TestWebApplicationFactory.IsolatedConnectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    private static NotificationService CreateService(
        ApplicationDbContext context)
    {
        return new NotificationService(
            context,
            new StubCurrentActorAccessor("candidate-30"),
            new FixedTimeProvider(
                new DateTimeOffset(
                    2026,
                    7,
                    25,
                    10,
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
}
