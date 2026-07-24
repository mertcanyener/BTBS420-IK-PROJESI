using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public const string TableName = "ActivityLogs";
    public const string AppendOnlyTriggerName = "TR_ActivityLogs_AppendOnly";

    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable(
            TableName,
            tableBuilder => tableBuilder.HasTrigger(AppendOnlyTriggerName));

        builder.HasKey(activityLog => activityLog.Id);

        builder.Property(activityLog => activityLog.OccurredAtUtc)
            .HasColumnType("datetimeoffset(7)")
            .IsRequired();

        builder.Property(activityLog => activityLog.ActorUserId)
            .HasMaxLength(450);

        builder.Property(activityLog => activityLog.ActionCode)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(activityLog => activityLog.TargetEntityType)
            .HasMaxLength(100);

        builder.Property(activityLog => activityLog.TargetEntityId)
            .HasMaxLength(128);

        builder.Property(activityLog => activityLog.JobPostingId)
            .HasMaxLength(128);

        builder.Property(activityLog => activityLog.CandidateId)
            .HasMaxLength(450);

        builder.Property(activityLog => activityLog.Summary)
            .HasMaxLength(ActivityLogRedactor.MaximumSummaryLength)
            .IsRequired();

        builder.HasIndex(activityLog => activityLog.OccurredAtUtc)
            .HasDatabaseName("IX_ActivityLogs_OccurredAtUtc");

        builder.HasIndex(
                activityLog => new
                {
                    activityLog.ActorUserId,
                    activityLog.OccurredAtUtc
                })
            .HasDatabaseName("IX_ActivityLogs_ActorUserId_OccurredAtUtc");

        builder.HasIndex(
                activityLog => new
                {
                    activityLog.ActionCode,
                    activityLog.OccurredAtUtc
                })
            .HasDatabaseName("IX_ActivityLogs_ActionCode_OccurredAtUtc");

        builder.HasIndex(
                activityLog => new
                {
                    activityLog.TargetEntityType,
                    activityLog.TargetEntityId,
                    activityLog.OccurredAtUtc
                })
            .HasDatabaseName(
                "IX_ActivityLogs_TargetEntityType_TargetEntityId_OccurredAtUtc");

        builder.HasIndex(
                activityLog => new
                {
                    activityLog.JobPostingId,
                    activityLog.OccurredAtUtc
                })
            .HasDatabaseName("IX_ActivityLogs_JobPostingId_OccurredAtUtc");

        builder.HasIndex(
                activityLog => new
                {
                    activityLog.CandidateId,
                    activityLog.OccurredAtUtc
                })
            .HasDatabaseName("IX_ActivityLogs_CandidateId_OccurredAtUtc");
    }
}
