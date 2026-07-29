using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public const string TableName = "Departments";

    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(department => department.Id);

        builder.Property(department => department.Name)
            .HasMaxLength(Department.MaximumNameLength)
            .IsRequired();

        builder.Property(department => department.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(department => department.Name)
            .IsUnique()
            .HasDatabaseName("UX_Departments_Name");
    }
}
