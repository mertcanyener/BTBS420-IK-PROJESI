using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BTBS420.RecruitmentSystem.Web.Identity;

public sealed class SampleRecruiterSeeder(
    UserManager<ApplicationUser> userManager,
    IOptions<IdentityBootstrapOptions> bootstrapOptions)
{
    private const string Email = "uzman@local.test";
    private const string Password = "Uzman123!Strong";

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
            if (!await userManager.IsInRoleAsync(existingUser, SystemRoles.RecruitmentSpecialist))
            {
                var addRoleResult = await userManager.AddToRoleAsync(
                    existingUser,
                    SystemRoles.RecruitmentSpecialist);
                EnsureSucceeded(addRoleResult, "Örnek İşe Alım Uzmanı rolü atanamadı");
            }

            return;
        }

        var recruiterUser = new ApplicationUser
        {
            UserName = Email,
            Email = Email,
            EmailConfirmed = true,
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(recruiterUser, Password);
        EnsureSucceeded(createResult, "Örnek İşe Alım Uzmanı kullanıcısı oluşturulamadı");

        var roleResult = await userManager.AddToRoleAsync(
            recruiterUser,
            SystemRoles.RecruitmentSpecialist);
        EnsureSucceeded(roleResult, "Örnek İşe Alım Uzmanı rolü atanamadı");
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
