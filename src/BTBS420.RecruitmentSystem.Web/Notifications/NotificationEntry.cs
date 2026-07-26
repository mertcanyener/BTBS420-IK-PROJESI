namespace BTBS420.RecruitmentSystem.Web.Notifications;

public sealed record NotificationEntry(
    string RecipientUserId,
    string EventKey,
    string Title,
    string Message);
