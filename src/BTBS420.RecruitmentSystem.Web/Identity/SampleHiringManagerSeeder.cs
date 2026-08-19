using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BTBS420.RecruitmentSystem.Web.Identity;

public sealed class SampleHiringManagerSeeder(
    UserManager<ApplicationUser> userManager,
    IOptions<IdentityBootstrapOptions> bootstrapOptions)
{
    private const string Email = "yonetici@local.test";
    private const string Password = "Yonetici123!Strong";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!bootstrapOptions.Value.Enabled)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var existingUser = await userManager.FindByEmailAsync(Email);

        if (existingUser is not null)
        {
            if (!await userManager.IsInRoleAsync(existingUser, SystemRoles.HiringManager))
            {
                var addRoleResult = await userManager.AddToRoleAsync(
                    existingUser,
                    SystemRoles.HiringManager);
                EnsureSucceeded(addRoleResult, "Örnek İşe Alım Yöneticisi rolü atanamadı");
            }

            return;
        }

        var managerUser = new ApplicationUser
        {
            UserName = Email,
            Email = Email,
            EmailConfirmed = true,
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(managerUser, Password);
        EnsureSucceeded(createResult, "Örnek İşe Alım Yöneticisi kullanıcısı oluşturulamadı");

        var roleResult = await userManager.AddToRoleAsync(
            managerUser,
            SystemRoles.HiringManager);
        EnsureSucceeded(roleResult, "Örnek İşe Alım Yöneticisi rolü atanamadı");
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
