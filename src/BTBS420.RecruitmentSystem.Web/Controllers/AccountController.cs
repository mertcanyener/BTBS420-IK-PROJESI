using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

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

    private const string DuplicateRegistrationMessage =
        "Kullanıcı adı veya e-posta zaten kullanılıyor.";

    private const string RegistrationFailedMessage =
        "Kayıt işlemi tamamlanamadı, lütfen tekrar deneyin.";

    internal const string RegistrationSuccessMessage =
        "Kaydınız oluşturuldu. Giriş yapabilirsiniz.";

    internal const string RegistrationSuccessTempDataKey = "RegistrationSuccessMessage";

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

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocalOrHome(returnUrl);
        }

        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(
        RegisterViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ClearPasswordsAndReturnView(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.UserName.Trim(),
            Email = model.Email.Trim()
        };

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        IdentityResult createResult;
        try
        {
            createResult = await userManager.CreateAsync(user, model.Password);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            ModelState.AddModelError(string.Empty, DuplicateRegistrationMessage);
            return ClearPasswordsAndReturnView(model);
        }

        if (!createResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);

            if (IsDuplicateError(createResult))
            {
                ModelState.AddModelError(string.Empty, DuplicateRegistrationMessage);
            }
            else
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return ClearPasswordsAndReturnView(model);
        }

        IdentityResult roleResult;
        try
        {
            roleResult = await userManager.AddToRoleAsync(user, SystemRoles.Candidate);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            ModelState.AddModelError(string.Empty, RegistrationFailedMessage);
            return ClearPasswordsAndReturnView(model);
        }
        catch (InvalidOperationException)
        {
            // UserStore.AddToRoleAsync, rol bulunamadığında IdentityResult.Failed
            // yerine InvalidOperationException fırlatır (ör. Candidate rolü henüz
            // seed edilmemişse). Bu, gerçek bir duplicate senaryosu değil, sistem/
            // altyapı hatasıdır; aynı genel başarısız-kayıt yoluna yönlendirilir.
            await transaction.RollbackAsync(cancellationToken);
            ModelState.AddModelError(string.Empty, RegistrationFailedMessage);
            return ClearPasswordsAndReturnView(model);
        }

        if (!roleResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            ModelState.AddModelError(string.Empty, RegistrationFailedMessage);
            return ClearPasswordsAndReturnView(model);
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.UserRegistered,
                "Aday hesabı oluşturuldu.",
                ActivityEntityTypes.User,
                user.Id));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        TempData[RegistrationSuccessTempDataKey] = RegistrationSuccessMessage;
        return RedirectToAction(nameof(Login));
    }

    private static bool IsDuplicateError(IdentityResult result)
    {
        return result.Errors.Any(
            error => error.Code is "DuplicateUserName" or "DuplicateEmail");
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException &&
            sqlException.Number is 2601 or 2627;
    }

    private IActionResult ClearPasswordsAndReturnView(RegisterViewModel model)
    {
        model.Password = string.Empty;
        model.ConfirmPassword = string.Empty;
        return View(model);
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
