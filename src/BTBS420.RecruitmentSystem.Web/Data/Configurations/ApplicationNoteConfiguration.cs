using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class ApplicationNoteConfiguration : IEntityTypeConfiguration<ApplicationNote>
{
    public const string TableName = "ApplicationNotes";

    public void Configure(EntityTypeBuilder<ApplicationNote> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(note => note.Id);

        builder.Property(note => note.AuthorUserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(note => note.Body)
            .HasMaxLength(ApplicationNote.MaximumBodyLength)
            .IsRequired();

        builder.HasIndex(note => note.JobApplicationId);

        builder.HasOne(note => note.JobApplication)
            .WithMany()
            .HasForeignKey(note => note.JobApplicationId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne(note => note.AuthorUser)
            .WithMany()
            .HasForeignKey(note => note.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
