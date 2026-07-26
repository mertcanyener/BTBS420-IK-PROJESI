using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTBS420.RecruitmentSystem.Web.Data.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public const string TableName = "Notifications";

    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.RecipientUserId)
            .HasMaxLength(Notification.MaximumRecipientUserIdLength)
            .IsRequired();

        builder.Property(notification => notification.EventKey)
            .HasMaxLength(Notification.MaximumEventKeyLength)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(notification => notification.Title)
            .HasMaxLength(Notification.MaximumTitleLength)
            .IsRequired();

        builder.Property(notification => notification.Message)
            .HasMaxLength(Notification.MaximumMessageLength)
            .IsRequired();

        builder.Property(notification => notification.CreatedAtUtc)
            .HasColumnType("datetimeoffset(7)")
            .IsRequired();

        builder.Property(notification => notification.ReadAtUtc)
            .HasColumnType("datetimeoffset(7)");

        builder.Ignore(notification => notification.IsRead);

        builder.HasOne(notification => notification.RecipientUser)
            .WithMany()
            .HasForeignKey(notification => notification.RecipientUserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasIndex(
                notification => new
                {
                    notification.RecipientUserId,
                    notification.EventKey
                })
            .IsUnique()
            .HasDatabaseName("UX_Notifications_RecipientUserId_EventKey");

        builder.HasIndex(
                notification => new
                {
                    notification.RecipientUserId,
                    notification.CreatedAtUtc,
                    notification.Id
                })
            .IsDescending(false, true, true)
            .HasDatabaseName(
                "IX_Notifications_RecipientUserId_CreatedAtUtc_Id");

        builder.HasIndex(notification => notification.RecipientUserId)
            .HasFilter("[ReadAtUtc] IS NULL")
            .HasDatabaseName("IX_Notifications_RecipientUserId_Unread");
    }
}
