using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class CandidateProfileLanguageConfiguration : IEntityTypeConfiguration<CandidateProfileLanguage>
{
    public const string TableName = "CandidateProfileLanguages";

    public void Configure(EntityTypeBuilder<CandidateProfileLanguage> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(link => new { link.CandidateProfileId, link.LanguageId });

        builder.HasOne(link => link.CandidateProfile)
            .WithMany()
            .HasForeignKey(link => link.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne(link => link.Language)
            .WithMany()
            .HasForeignKey(link => link.LanguageId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
