using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class InterviewConfiguration : IEntityTypeConfiguration<Interview>
{
    public const string TableName = "Interviews";

    public void Configure(EntityTypeBuilder<Interview> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(interview => interview.Id);

        builder.Property(interview => interview.InterviewType)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(interview => interview.Status)
            .HasMaxLength(32)
            .HasDefaultValue(InterviewStatuses.Scheduled)
            .IsRequired();

        builder.Property(interview => interview.OnlineMeetingLink)
            .HasMaxLength(Interview.MaximumOnlineMeetingLinkLength);

        builder.Property(interview => interview.Location)
            .HasMaxLength(Interview.MaximumLocationLength);

        builder.Property(interview => interview.RowVersion)
            .IsRowVersion();

        builder.HasIndex(interview => interview.JobApplicationId);

        builder.HasOne(interview => interview.JobApplication)
            .WithMany()
            .HasForeignKey(interview => interview.JobApplicationId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
