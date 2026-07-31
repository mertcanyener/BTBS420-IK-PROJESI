using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public const string TableName = "JobApplications";

    public void Configure(EntityTypeBuilder<JobApplication> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(application => application.Id);

        builder.HasIndex(application => new { application.JobPostingId, application.CandidateProfileId })
            .IsUnique()
            .HasDatabaseName("UX_JobApplications_JobPostingId_CandidateProfileId");

        builder.HasOne(application => application.JobPosting)
            .WithMany()
            .HasForeignKey(application => application.JobPostingId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne(application => application.CandidateProfile)
            .WithMany()
            .HasForeignKey(application => application.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
