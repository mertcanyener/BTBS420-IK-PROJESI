using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class JobPostingConfiguration : IEntityTypeConfiguration<JobPosting>
{
    public const string TableName = "JobPostings";

    public void Configure(EntityTypeBuilder<JobPosting> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(jobPosting => jobPosting.Id);

        builder.Property(jobPosting => jobPosting.Title)
            .HasMaxLength(JobPosting.MaximumTitleLength)
            .IsRequired();

        builder.Property(jobPosting => jobPosting.Description)
            .HasMaxLength(JobPosting.MaximumDescriptionLength)
            .IsRequired();

        builder.Property(jobPosting => jobPosting.ResponsibleUserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(jobPosting => jobPosting.ApplicationDeadline)
            .IsRequired();

        builder.Property(jobPosting => jobPosting.Status)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(jobPosting => jobPosting.IsInternal)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(jobPosting => jobPosting.RowVersion)
            .IsRowVersion();

        builder.HasOne(jobPosting => jobPosting.Position)
            .WithMany()
            .HasForeignKey(jobPosting => jobPosting.PositionId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne(jobPosting => jobPosting.ResponsibleUser)
            .WithMany()
            .HasForeignKey(jobPosting => jobPosting.ResponsibleUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
