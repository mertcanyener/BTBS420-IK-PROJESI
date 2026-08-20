using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class CandidateDocumentConfiguration : IEntityTypeConfiguration<CandidateDocument>
{
    public const string TableName = "CandidateDocuments";

    public void Configure(EntityTypeBuilder<CandidateDocument> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(document => document.Id);

        builder.Property(document => document.DocumentType)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(document => document.OriginalFileName)
            .HasMaxLength(CandidateDocument.MaximumOriginalFileNameLength)
            .IsRequired();

        builder.Property(document => document.StoredFileName)
            .HasMaxLength(CandidateDocument.MaximumStoredFileNameLength)
            .IsRequired();

        builder.Property(document => document.ContentType)
            .HasMaxLength(CandidateDocument.MaximumContentTypeLength)
            .IsRequired();

        builder.HasIndex(document => document.CandidateProfileId);

        builder.HasOne(document => document.CandidateProfile)
            .WithMany()
            .HasForeignKey(document => document.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
