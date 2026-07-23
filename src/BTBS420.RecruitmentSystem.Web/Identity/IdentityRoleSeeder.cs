using BTBS420.RecruitmentSystem.Web.Authorization;
using Microsoft.AspNetCore.Identity;

namespace BTBS420.RecruitmentSystem.Web.Identity;

public sealed class IdentityRoleSeeder(RoleManager<IdentityRole> roleManager)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var roleName in SystemRoles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(
                new IdentityRole
                {
                    Name = roleName
                });

            if (result.Succeeded || await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            throw CreateSeedException(roleName, result);
        }
    }

    private static InvalidOperationException CreateSeedException(
        string roleName,
        IdentityResult result)
    {
        var errorCodes = string.Join(
            ", ",
            result.Errors.Select(error => error.Code));

        return new InvalidOperationException(
            $"'{roleName}' rolü oluşturulamadı. Identity hata kodları: {errorCodes}");
    }
}
