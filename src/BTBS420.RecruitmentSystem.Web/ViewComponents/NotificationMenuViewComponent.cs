using BTBS420.RecruitmentSystem.Web.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTBS420.RecruitmentSystem.Web.ViewComponents;

public sealed class NotificationMenuViewComponent(
    INotificationCenterService notificationCenterService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (HttpContext.User.Identity?.IsAuthenticated != true ||
            HttpContext.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            return Content(string.Empty);
        }

        var unreadCount = await notificationCenterService.GetUnreadCountAsync(
            HttpContext.RequestAborted);

        return View(unreadCount);
    }
}
