using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Data.Configurations;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class ActivityLogInfrastructureTests :
    IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ActivityLogInfrastructureTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void ApplicationDbContext_ActivityLogModeliniVeFiltreIndeksleriniIcerir()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entityType = context.Model.FindEntityType(typeof(ActivityLog));

        Assert.NotNull(entityType);
        Assert.Equal(ActivityLogConfiguration.TableName, entityType.GetTableName());
        Assert.Equal(
            "datetimeoffset(7)",
            entityType.FindProperty(nameof(ActivityLog.OccurredAtUtc))?.GetColumnType());
        Assert.Equal(
            450,
            entityType.FindProperty(nameof(ActivityLog.ActorUserId))?.GetMaxLength());
        Assert.Equal(
            100,
            entityType.FindProperty(nameof(ActivityLog.ActionCode))?.GetMaxLength());
        Assert.Equal(
            100,
            entityType.FindProperty(nameof(ActivityLog.TargetEntityType))?.GetMaxLength());
        Assert.Equal(
            128,
            entityType.FindProperty(nameof(ActivityLog.TargetEntityId))?.GetMaxLength());
        Assert.Equal(
            128,
            entityType.FindProperty(nameof(ActivityLog.JobPostingId))?.GetMaxLength());
        Assert.Equal(
            450,
            entityType.FindProperty(nameof(ActivityLog.CandidateId))?.GetMaxLength());
        Assert.Equal(
            ActivityLogRedactor.MaximumSummaryLength,
            entityType.FindProperty(nameof(ActivityLog.Summary))?.GetMaxLength());

        var indexes = entityType
            .GetIndexes()
            .ToDictionary(
                index => index.GetDatabaseName()!,
                index => index.Properties.Select(property => property.Name).ToArray(),
                StringComparer.Ordinal);

        Assert.Equal(
            [nameof(ActivityLog.OccurredAtUtc)],
            indexes["IX_ActivityLogs_OccurredAtUtc"]);
        Assert.Equal(
            [nameof(ActivityLog.ActorUserId), nameof(ActivityLog.OccurredAtUtc)],
            indexes["IX_ActivityLogs_ActorUserId_OccurredAtUtc"]);
        Assert.Equal(
            [nameof(ActivityLog.ActionCode), nameof(ActivityLog.OccurredAtUtc)],
            indexes["IX_ActivityLogs_ActionCode_OccurredAtUtc"]);
        Assert.Equal(
            [
                nameof(ActivityLog.TargetEntityType),
                nameof(ActivityLog.TargetEntityId),
                nameof(ActivityLog.OccurredAtUtc)
            ],
            indexes[
                "IX_ActivityLogs_TargetEntityType_TargetEntityId_OccurredAtUtc"]);
        Assert.Equal(
            [nameof(ActivityLog.JobPostingId), nameof(ActivityLog.OccurredAtUtc)],
            indexes["IX_ActivityLogs_JobPostingId_OccurredAtUtc"]);
        Assert.Equal(
            [nameof(ActivityLog.CandidateId), nameof(ActivityLog.OccurredAtUtc)],
            indexes["IX_ActivityLogs_CandidateId_OccurredAtUtc"]);
        Assert.Contains(
            entityType.GetDeclaredTriggers(),
            trigger => trigger.ModelName == ActivityLogConfiguration.AppendOnlyTriggerName);
    }

    [Fact]
    public void ApplicationDbContext_AddActivityLogInfrastructureMigrationiniIcerir()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Contains(
            context.Database.GetMigrations(),
            migration => migration.EndsWith(
                "_AddActivityLogInfrastructure",
                StringComparison.Ordinal));
    }

    [Fact]
    public void ActivityLogServisleriScopedContextIleCozumlenir()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IActivityLogService>();

        var activityLog = service.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityCreated,
                "Servis kayıt testi.",
                ActivityEntityTypes.System,
                "kan23"));

        Assert.Same(context, scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        Assert.Equal(EntityState.Added, context.Entry(activityLog).State);
    }

    [Fact]
    public void ActivityLog_GuncellemeGirisiminiSaveChangesOncesindeReddeder()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IActivityLogService>();
        var activityLog = service.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityUpdated,
                "Güncelleme koruması.",
                ActivityEntityTypes.System,
                "kan23"));

        MarkAsPersisted(context, activityLog, id: 23);
        context.Entry(activityLog).State = EntityState.Modified;

        var exception = Assert.Throws<InvalidOperationException>(
            () => context.SaveChanges());

        Assert.Contains("güncellenemez veya silinemez", exception.Message);
        Assert.Equal(
            System.Data.ConnectionState.Closed,
            context.Database.GetDbConnection().State);
    }

    [Fact]
    public async Task ActivityLog_SilmeGirisiminiSaveChangesAsyncOncesindeReddeder()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IActivityLogService>();
        var activityLog = service.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityDeleted,
                "Silme koruması.",
                ActivityEntityTypes.System,
                "kan23"));

        MarkAsPersisted(context, activityLog, id: 24);
        context.Entry(activityLog).State = EntityState.Deleted;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync());

        Assert.Contains("güncellenemez veya silinemez", exception.Message);
        Assert.Equal(
            System.Data.ConnectionState.Closed,
            context.Database.GetDbConnection().State);
    }

    [Fact]
    public void ActivityLog_DisisalKodTarafindanDegistirilebilirSetterSunmaz()
    {
        var writableProperties = typeof(ActivityLog)
            .GetProperties()
            .Where(property => property.SetMethod?.IsPublic == true);

        Assert.Empty(writableProperties);
    }

    [Fact]
    public void ActivityLogIcinUpdateVeyaDeleteControllerActioniYoktur()
    {
        var actionProvider =
            _factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>();
        var mutationActions = actionProvider.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(
                action =>
                    action.ControllerName.Contains(
                        "ActivityLog",
                        StringComparison.OrdinalIgnoreCase) &&
                    (
                        action.ActionName.Contains(
                            "Update",
                            StringComparison.OrdinalIgnoreCase) ||
                        action.ActionName.Contains(
                            "Edit",
                            StringComparison.OrdinalIgnoreCase) ||
                        action.ActionName.Contains(
                            "Delete",
                            StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        Assert.Empty(mutationActions);
    }

    private static void MarkAsPersisted(
        ApplicationDbContext context,
        ActivityLog activityLog,
        long id)
    {
        var idProperty = context
            .Entry(activityLog)
            .Property(log => log.Id);

        idProperty.CurrentValue = id;
        idProperty.IsTemporary = false;
        context.Entry(activityLog).State = EntityState.Unchanged;
    }
}
