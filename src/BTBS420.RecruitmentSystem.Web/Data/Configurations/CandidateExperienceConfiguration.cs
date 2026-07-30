using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class CandidateExperienceConfiguration : IEntityTypeConfiguration<CandidateExperience>
{
    public const string TableName = "CandidateExperiences";

    public void Configure(EntityTypeBuilder<CandidateExperience> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(experience => experience.Id);

        builder.Property(experience => experience.CompanyName)
            .HasMaxLength(CandidateExperience.MaximumCompanyNameLength)
            .IsRequired();

        builder.Property(experience => experience.JobTitle)
            .HasMaxLength(CandidateExperience.MaximumJobTitleLength)
            .IsRequired();

        builder.HasIndex(experience => experience.CandidateProfileId);

        builder.HasOne(experience => experience.CandidateProfile)
            .WithMany()
            .HasForeignKey(experience => experience.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
