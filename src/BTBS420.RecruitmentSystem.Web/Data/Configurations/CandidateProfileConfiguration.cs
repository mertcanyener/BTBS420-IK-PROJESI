using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class CandidateProfileConfiguration : IEntityTypeConfiguration<CandidateProfile>
{
    public const string TableName = "CandidateProfiles";

    public void Configure(EntityTypeBuilder<CandidateProfile> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.ApplicationUserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(profile => profile.FirstName)
            .HasMaxLength(CandidateProfile.MaximumFirstNameLength)
            .IsRequired();

        builder.Property(profile => profile.LastName)
            .HasMaxLength(CandidateProfile.MaximumLastNameLength)
            .IsRequired();

        builder.Property(profile => profile.ProfessionalSummary)
            .HasMaxLength(CandidateProfile.MaximumProfessionalSummaryLength)
            .IsRequired();

        builder.HasIndex(profile => profile.ApplicationUserId)
            .IsUnique()
            .HasDatabaseName("UX_CandidateProfiles_ApplicationUserId");

        builder.HasOne(profile => profile.ApplicationUser)
            .WithMany()
            .HasForeignKey(profile => profile.ApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne(profile => profile.TargetPosition)
            .WithMany()
            .HasForeignKey(profile => profile.TargetPositionId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
