using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class SeniorityConfiguration : IEntityTypeConfiguration<Seniority>
{
    public const string TableName = "Seniorities";

    public void Configure(EntityTypeBuilder<Seniority> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(seniority => seniority.Id);

        builder.Property(seniority => seniority.Name)
            .HasMaxLength(Seniority.MaximumNameLength)
            .IsRequired();

        builder.Property(seniority => seniority.Rank)
            .IsRequired();

        builder.Property(seniority => seniority.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(seniority => seniority.Name)
            .IsUnique()
            .HasDatabaseName("UX_Seniorities_Name");

        builder.HasIndex(seniority => seniority.Rank)
            .IsUnique()
            .HasDatabaseName("UX_Seniorities_Rank");
    }
}
