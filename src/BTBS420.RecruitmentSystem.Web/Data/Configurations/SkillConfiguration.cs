using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public const string TableName = "Skills";

    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(skill => skill.Id);

        builder.Property(skill => skill.Name)
            .HasMaxLength(Skill.MaximumNameLength)
            .IsRequired();

        builder.Property(skill => skill.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(skill => skill.Name)
            .IsUnique()
            .HasDatabaseName("UX_Skills_Name");
    }
}
