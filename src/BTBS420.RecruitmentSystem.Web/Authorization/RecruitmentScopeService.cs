using System.Security.Claims;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace BTBS420.RecruitmentSystem.Web.Authorization;

public sealed class RecruitmentScopeService(UserManager<ApplicationUser> userManager) : IRecruitmentScopeService
{
    public async Task<RecruitmentScope?> GetScopeAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (principal.IsInRole(SystemRoles.Admin))
        {
            return RecruitmentScope.Unrestricted;
        }

        var currentUser = await userManager.GetUserAsync(principal);
        if (currentUser is null)
        {
            return null;
        }

        if (principal.IsInRole(SystemRoles.RecruitmentSpecialist))
        {
            return RecruitmentScope.ForResponsibleUser(currentUser.Id);
        }

        if (principal.IsInRole(SystemRoles.HiringManager))
        {
            return RecruitmentScope.ForDepartment(currentUser.DepartmentId);
        }

        return null;
    }
}
