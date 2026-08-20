using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class InterviewEvaluationConfiguration : IEntityTypeConfiguration<InterviewEvaluation>
{
    public const string TableName = "InterviewEvaluations";

    public void Configure(EntityTypeBuilder<InterviewEvaluation> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(evaluation => evaluation.Id);

        builder.HasIndex(evaluation => new { evaluation.InterviewId, evaluation.EvaluatorUserId })
            .IsUnique()
            .HasDatabaseName("UX_InterviewEvaluations_InterviewId_EvaluatorUserId");

        builder.Property(evaluation => evaluation.Note)
            .HasMaxLength(InterviewEvaluation.MaximumNoteLength);

        builder.Property(evaluation => evaluation.Recommendation)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(evaluation => evaluation.RowVersion)
            .IsRowVersion();

        builder.HasOne(evaluation => evaluation.Interview)
            .WithMany()
            .HasForeignKey(evaluation => evaluation.InterviewId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(evaluation => evaluation.EvaluatorUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
