using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public const string TableName = "Locations";

    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(location => location.Id);

        builder.Property(location => location.Name)
            .HasMaxLength(Location.MaximumNameLength)
            .IsRequired();

        builder.Property(location => location.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(location => location.Name)
            .IsUnique()
            .HasDatabaseName("UX_Locations_Name");
    }
}
