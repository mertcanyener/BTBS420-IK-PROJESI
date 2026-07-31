using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class InterviewParticipantConfiguration : IEntityTypeConfiguration<InterviewParticipant>
{
    public const string TableName = "InterviewParticipants";

    public void Configure(EntityTypeBuilder<InterviewParticipant> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(participant => participant.Id);

        builder.Property(participant => participant.ParticipantUserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.HasIndex(participant => new { participant.InterviewId, participant.ParticipantUserId })
            .IsUnique()
            .HasDatabaseName("UX_InterviewParticipants_InterviewId_ParticipantUserId");

        builder.HasOne(participant => participant.Interview)
            .WithMany()
            .HasForeignKey(participant => participant.InterviewId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne(participant => participant.ParticipantUser)
            .WithMany()
            .HasForeignKey(participant => participant.ParticipantUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
