using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BTBS420.RecruitmentSystem.Web.Identity;

public sealed class ApplicationSecurityStampValidator(
    IOptions<SecurityStampValidatorOptions> options,
    SignInManager<ApplicationUser> signInManager,
    ILoggerFactory loggerFactory)
    : SecurityStampValidator<ApplicationUser>(options, signInManager, loggerFactory)
{
    public override async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        await base.ValidateAsync(context);

        if (context.Principal?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userManager = context.HttpContext.RequestServices
            .GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.GetUserAsync(context.Principal);

        if (user is not null && user.IsActive)
        {
            return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

        var dbContext = context.HttpContext.RequestServices
            .GetRequiredService<ApplicationDbContext>();
        var activityLogService = context.HttpContext.RequestServices
            .GetRequiredService<IActivityLogService>();

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.AuthenticationFailed,
                "Başarısız kimlik doğrulama denemesi."));
        await dbContext.SaveChangesAsync();
    }
}
