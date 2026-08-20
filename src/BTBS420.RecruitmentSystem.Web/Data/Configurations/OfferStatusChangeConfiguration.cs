using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class OfferStatusChangeConfiguration : IEntityTypeConfiguration<OfferStatusChange>
{
    public const string TableName = "OfferStatusChanges";

    public void Configure(EntityTypeBuilder<OfferStatusChange> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(change => change.Id);

        builder.Property(change => change.FromStatus)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(change => change.ToStatus)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(change => change.ActorUserId)
            .IsRequired();

        builder.Property(change => change.Reason)
            .HasMaxLength(OfferStatusChange.MaximumReasonLength);

        builder.HasIndex(change => change.OfferId);

        builder.HasOne(change => change.Offer)
            .WithMany()
            .HasForeignKey(change => change.OfferId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(change => change.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
