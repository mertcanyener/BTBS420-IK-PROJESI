using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class JobFamilyConfiguration : IEntityTypeConfiguration<JobFamily>
{
    public const string TableName = "JobFamilies";

    public void Configure(EntityTypeBuilder<JobFamily> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(jobFamily => jobFamily.Id);

        builder.Property(jobFamily => jobFamily.Name)
            .HasMaxLength(JobFamily.MaximumNameLength)
            .IsRequired();

        builder.Property(jobFamily => jobFamily.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(jobFamily => jobFamily.Name)
            .IsUnique()
            .HasDatabaseName("UX_JobFamilies_Name");
    }
}
