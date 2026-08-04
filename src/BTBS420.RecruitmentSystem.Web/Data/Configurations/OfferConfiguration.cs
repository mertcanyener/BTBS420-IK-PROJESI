using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public const string TableName = "Offers";

    public void Configure(EntityTypeBuilder<Offer> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(offer => offer.Id);

        builder.Property(offer => offer.Status)
            .HasMaxLength(32)
            .HasDefaultValue(OfferStatuses.Draft)
            .IsRequired();

        builder.Property(offer => offer.Salary)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(offer => offer.StartDate)
            .IsRequired();

        builder.Property(offer => offer.Note)
            .HasMaxLength(Offer.MaximumNoteLength);

        builder.Property(offer => offer.CreatedByUserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(offer => offer.RowVersion)
            .IsRowVersion();

        builder.HasIndex(offer => offer.JobApplicationId)
            .IsUnique();

        builder.HasOne(offer => offer.JobApplication)
            .WithMany()
            .HasForeignKey(offer => offer.JobApplicationId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne(offer => offer.CreatedByUser)
            .WithMany()
            .HasForeignKey(offer => offer.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
