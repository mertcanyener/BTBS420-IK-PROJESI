using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class LanguageConfiguration : IEntityTypeConfiguration<Language>
{
    public const string TableName = "Languages";

    public void Configure(EntityTypeBuilder<Language> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(language => language.Id);

        builder.Property(language => language.Name)
            .HasMaxLength(Language.MaximumNameLength)
            .IsRequired();

        builder.Property(language => language.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(language => language.Name)
            .IsUnique()
            .HasDatabaseName("UX_Languages_Name");
    }
}
