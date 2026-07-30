using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class CandidateEducationConfiguration : IEntityTypeConfiguration<CandidateEducation>
{
    public const string TableName = "CandidateEducations";

    public void Configure(EntityTypeBuilder<CandidateEducation> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(education => education.Id);

        builder.Property(education => education.SchoolName)
            .HasMaxLength(CandidateEducation.MaximumSchoolNameLength)
            .IsRequired();

        builder.Property(education => education.FieldOfStudy)
            .HasMaxLength(CandidateEducation.MaximumFieldOfStudyLength);

        builder.HasIndex(education => education.CandidateProfileId);

        builder.HasOne(education => education.CandidateProfile)
            .WithMany()
            .HasForeignKey(education => education.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne(education => education.Education)
            .WithMany()
            .HasForeignKey(education => education.EducationId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
