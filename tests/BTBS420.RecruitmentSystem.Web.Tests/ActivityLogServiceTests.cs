using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class ActivityLogServiceTests
{
    [Fact]
    public void Stage_AyniContextteUtcActorBaglamVeRedakteOzetOlusturur()
    {
        using var context = CreateContext();
        var expectedUtc = new DateTimeOffset(
            2026,
            7,
            24,
            10,
            30,
            0,
            TimeSpan.Zero);
        var service = CreateService(
            context,
            actorUserId: " kan23-actor ",
            expectedUtc);

        var activityLog = service.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "Başvuru güncellendi password=KAN23-Secret",
                ActivityEntityTypes.Application,
                "application-23",
                "job-23",
                "candidate-23"));

        Assert.Equal(expectedUtc, activityLog.OccurredAtUtc);
        Assert.Equal("kan23-actor", activityLog.ActorUserId);
        Assert.Equal(ActivityActionCodes.EntityStatusChanged, activityLog.ActionCode);
        Assert.Equal(ActivityEntityTypes.Application, activityLog.TargetEntityType);
        Assert.Equal("application-23", activityLog.TargetEntityId);
        Assert.Equal("job-23", activityLog.JobPostingId);
        Assert.Equal("candidate-23", activityLog.CandidateId);
        Assert.Contains(ActivityLogRedactor.RedactedValue, activityLog.Summary);
        Assert.DoesNotContain("KAN23-Secret", activityLog.Summary);
        Assert.Equal(EntityState.Added, context.Entry(activityLog).State);
        Assert.Equal(0, activityLog.Id);
    }

    [Fact]
    public void Stage_KendiBasinaSaveChangesVeyaVeritabaniBaglantisiAcmaz()
    {
        using var context = CreateContext();
        var service = CreateService(
            context,
            actorUserId: null,
            DateTimeOffset.UnixEpoch);

        service.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityCreated,
                "Güvenli kısa özet.",
                ActivityEntityTypes.User,
                "user-23"));

        Assert.Single(context.ChangeTracker.Entries<ActivityLog>());
        Assert.Equal(
            System.Data.ConnectionState.Closed,
            context.Database.GetDbConnection().State);
    }

    [Theory]
    [InlineData("tanimsiz.islem", ActivityEntityTypes.User, "user-23")]
    [InlineData(ActivityActionCodes.EntityCreated, "tanimsiz-hedef", "user-23")]
    [InlineData(ActivityActionCodes.EntityCreated, null, "user-23")]
    public void Stage_TanimsizKodVeyaEksikHedefBaglaminiReddeder(
        string actionCode,
        string? targetEntityType,
        string? targetEntityId)
    {
        using var context = CreateContext();
        var service = CreateService(
            context,
            actorUserId: null,
            DateTimeOffset.UnixEpoch);

        Assert.Throws<ArgumentException>(
            () => service.Stage(
                new ActivityLogEntry(
                    actionCode,
                    "Özet",
                    targetEntityType,
                    targetEntityId)));
        Assert.Empty(context.ChangeTracker.Entries<ActivityLog>());
    }

    [Fact]
    public void Stage_KontrolKarakterliBaglamKimliginiReddeder()
    {
        using var context = CreateContext();
        var service = CreateService(
            context,
            actorUserId: null,
            DateTimeOffset.UnixEpoch);

        Assert.Throws<ArgumentException>(
            () => service.Stage(
                new ActivityLogEntry(
                    ActivityActionCodes.EntityUpdated,
                    "Özet",
                    ActivityEntityTypes.Candidate,
                    "candidate\r\nspoof")));
    }

    [Theory]
    [InlineData("token=KAN23-Secret")]
    [InlineData("candidate\u202Espoof")]
    [InlineData("candidate\u2028spoof")]
    public void Stage_HassasVeyaUnicodeBaglamKimliginiReddeder(
        string targetEntityId)
    {
        using var context = CreateContext();
        var service = CreateService(
            context,
            actorUserId: null,
            DateTimeOffset.UnixEpoch);

        Assert.Throws<ArgumentException>(
            () => service.Stage(
                new ActivityLogEntry(
                    ActivityActionCodes.EntityUpdated,
                    "Özet",
                    ActivityEntityTypes.Candidate,
                    targetEntityId)));
        Assert.Empty(context.ChangeTracker.Entries<ActivityLog>());
    }

    [Fact]
    public void Stage_HassasActorKimliginiReddeder()
    {
        using var context = CreateContext();
        var service = CreateService(
            context,
            actorUserId: "token=KAN23-ActorSecret",
            DateTimeOffset.UnixEpoch);

        Assert.Throws<ArgumentException>(
            () => service.Stage(
                new ActivityLogEntry(
                    ActivityActionCodes.EntityUpdated,
                    "Özet",
                    ActivityEntityTypes.Candidate,
                    "candidate-23")));
        Assert.Empty(context.ChangeTracker.Entries<ActivityLog>());
    }

    [Fact]
    public void MerkeziIslemVeHedefKodlariBenzersizdir()
    {
        Assert.Equal(
            ActivityActionCodes.All.Count,
            ActivityActionCodes.All.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            ActivityEntityTypes.All.Count,
            ActivityEntityTypes.All.Distinct(StringComparer.Ordinal).Count());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(TestWebApplicationFactory.IsolatedConnectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    private static ActivityLogService CreateService(
        ApplicationDbContext context,
        string? actorUserId,
        DateTimeOffset utcNow)
    {
        return new ActivityLogService(
            context,
            new StubCurrentActorAccessor(actorUserId),
            new ActivityLogRedactor(),
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
}
