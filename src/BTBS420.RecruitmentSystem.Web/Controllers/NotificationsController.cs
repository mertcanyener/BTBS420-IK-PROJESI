using BTBS420.RecruitmentSystem.Web.Notifications;
using BTBS420.RecruitmentSystem.Web.ViewModels.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize]
public sealed class NotificationsController(
    INotificationCenterService notificationCenterService) : Controller
{
    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var notifications =
            await notificationCenterService.GetNotificationsAsync(cancellationToken);

        return View(new NotificationIndexViewModel(notifications));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(
        long id,
        CancellationToken cancellationToken)
    {
        if (id <= 0 ||
            !await notificationCenterService.MarkAsReadAsync(id, cancellationToken))
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        await notificationCenterService.MarkAllAsReadAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }
}
