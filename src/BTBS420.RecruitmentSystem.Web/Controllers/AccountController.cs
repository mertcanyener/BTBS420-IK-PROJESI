using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

public sealed class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IActivityLogService activityLogService,
    ApplicationDbContext dbContext) : Controller
{
    private const string InvalidCredentialsMessage =
        "Kullanıcı adı/e-posta veya parola hatalı.";

    private const string FailedAuthenticationSummary =
        "Başarısız kimlik doğrulama denemesi.";

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocalOrHome(returnUrl);
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.Password = string.Empty;
            return View(model);
        }

        var user = await ResolveUserAsync(model.UsernameOrEmail);
        var succeeded = false;

        if (user is not null && user.IsActive)
        {
            var signInResult = await signInManager.CheckPasswordSignInAsync(
                user,
                model.Password,
                lockoutOnFailure: true);
            succeeded = signInResult.Succeeded;
        }

        if (succeeded && user is not null)
        {
            await signInManager.SignInAsync(user, isPersistent: false);

            activityLogService.Stage(
                new ActivityLogEntry(
                    ActivityActionCodes.AuthenticationSucceeded,
                    "Kullanıcı başarıyla giriş yaptı.",
                    ActivityEntityTypes.User,
                    user.Id));
            await dbContext.SaveChangesAsync(cancellationToken);

            return RedirectToLocalOrHome(model.ReturnUrl);
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.AuthenticationFailed,
                FailedAuthenticationSummary));
        await dbContext.SaveChangesAsync(cancellationToken);

        ModelState.AddModelError(string.Empty, InvalidCredentialsMessage);
        model.Password = string.Empty;
        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.AuthenticationSignedOut,
                "Kullanıcı çıkış yaptı.",
                ActivityEntityTypes.User,
                userId));
        await dbContext.SaveChangesAsync(cancellationToken);

        await signInManager.SignOutAsync();

        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    private async Task<ApplicationUser?> ResolveUserAsync(string usernameOrEmail)
    {
        var trimmed = usernameOrEmail.Trim();

        return await userManager.FindByNameAsync(trimmed)
            ?? await userManager.FindByEmailAsync(trimmed);
    }

    private IActionResult RedirectToLocalOrHome(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(nameof(HomeController.Index), "Home");
    }
}
