using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class EducationConfiguration : IEntityTypeConfiguration<Education>
{
    public const string TableName = "Educations";

    public void Configure(EntityTypeBuilder<Education> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(education => education.Id);

        builder.Property(education => education.Name)
            .HasMaxLength(Education.MaximumNameLength)
            .IsRequired();

        builder.Property(education => education.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(education => education.Name)
            .IsUnique()
            .HasDatabaseName("UX_Educations_Name");
    }
}
