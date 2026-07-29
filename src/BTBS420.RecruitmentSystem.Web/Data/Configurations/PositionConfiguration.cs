using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public const string TableName = "Positions";

    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(position => position.Id);

        builder.Property(position => position.Name)
            .HasMaxLength(Position.MaximumNameLength)
            .IsRequired();

        builder.Property(position => position.IsActive)
            .HasDefaultValue(true);

        builder.HasOne(position => position.Department)
            .WithMany()
            .HasForeignKey(position => position.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne(position => position.JobFamily)
            .WithMany()
            .HasForeignKey(position => position.JobFamilyId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(position => position.Seniority)
            .WithMany()
            .HasForeignKey(position => position.SeniorityId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(
                position => new { position.DepartmentId, position.Name })
            .IsUnique()
            .HasDatabaseName("UX_Positions_DepartmentId_Name");
    }
}
