using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class ExperienceRangeConfiguration : IEntityTypeConfiguration<ExperienceRange>
{
    public const string TableName = "ExperienceRanges";

    public void Configure(EntityTypeBuilder<ExperienceRange> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(experienceRange => experienceRange.Id);

        builder.Property(experienceRange => experienceRange.Name)
            .HasMaxLength(ExperienceRange.MaximumNameLength)
            .IsRequired();

        builder.Property(experienceRange => experienceRange.MinYears)
            .IsRequired();

        builder.Property(experienceRange => experienceRange.MaxYears)
            .IsRequired();

        builder.Property(experienceRange => experienceRange.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(experienceRange => experienceRange.Name)
            .IsUnique()
            .HasDatabaseName("UX_ExperienceRanges_Name");
    }
}
