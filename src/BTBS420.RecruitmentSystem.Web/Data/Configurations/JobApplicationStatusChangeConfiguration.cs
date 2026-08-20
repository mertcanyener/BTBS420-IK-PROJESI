using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class JobApplicationStatusChangeConfiguration :
    IEntityTypeConfiguration<JobApplicationStatusChange>
{
    public const string TableName = "JobApplicationStatusChanges";

    public void Configure(EntityTypeBuilder<JobApplicationStatusChange> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(change => change.Id);

        builder.Property(change => change.FromStatus)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(change => change.ToStatus)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(change => change.ActorUserId)
            .IsRequired();

        builder.Property(change => change.Reason)
            .HasMaxLength(JobApplicationStatusChange.MaximumReasonLength);

        builder.HasIndex(change => change.JobApplicationId);

        builder.HasOne(change => change.JobApplication)
            .WithMany()
            .HasForeignKey(change => change.JobApplicationId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(change => change.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
