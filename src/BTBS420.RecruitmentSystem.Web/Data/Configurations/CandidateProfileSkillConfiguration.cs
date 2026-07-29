using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class CandidateProfileSkillConfiguration : IEntityTypeConfiguration<CandidateProfileSkill>
{
    public const string TableName = "CandidateProfileSkills";

    public void Configure(EntityTypeBuilder<CandidateProfileSkill> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(link => new { link.CandidateProfileId, link.SkillId });

        builder.HasOne(link => link.CandidateProfile)
            .WithMany()
            .HasForeignKey(link => link.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne(link => link.Skill)
            .WithMany()
            .HasForeignKey(link => link.SkillId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
