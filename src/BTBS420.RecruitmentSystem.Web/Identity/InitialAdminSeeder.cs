using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BTBS420.RecruitmentSystem.Web.Identity;

public sealed class InitialAdminSeeder(
    UserManager<ApplicationUser> userManager,
    IOptions<IdentityBootstrapOptions> bootstrapOptions)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var options = bootstrapOptions.Value;
        options.Validate();

        if (!options.Enabled)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var email = options.AdminEmail!.Trim();
        var existingUser = await userManager.FindByEmailAsync(email);

        if (existingUser is not null)
        {
            if (await userManager.IsInRoleAsync(existingUser, SystemRoles.Admin))
            {
                return;
            }

            throw new InvalidOperationException(
                "Yapılandırılan ilk Admin e-postası mevcut bir kullanıcıya ait. " +
                "Güvenlik nedeniyle mevcut kullanıcıya otomatik rol yükseltmesi yapılmadı.");
        }

        var adminUser = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(
            adminUser,
            options.AdminPassword!);
        EnsureSucceeded(createResult, "İlk Admin kullanıcısı oluşturulamadı");

        var roleResult = await userManager.AddToRoleAsync(
            adminUser,
            SystemRoles.Admin);
        EnsureSucceeded(roleResult, "İlk Admin rolü atanamadı");
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errorCodes = string.Join(
            ", ",
            result.Errors.Select(error => error.Code));

        throw new InvalidOperationException(
            $"{operation}. Identity hata kodları: {errorCodes}");
    }
}
