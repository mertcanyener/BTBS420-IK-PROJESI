using System.Security.Claims;

namespace BTBS420.RecruitmentSystem.Web.Authorization;

public interface IRecruitmentScopeService
{
    Task<RecruitmentScope?> GetScopeAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}
