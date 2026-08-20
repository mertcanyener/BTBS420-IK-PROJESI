using System.Security.Claims;

namespace BTBS420.RecruitmentSystem.Web.ActivityLogging;

public sealed class HttpContextCurrentActorAccessor(
    IHttpContextAccessor httpContextAccessor) : ICurrentActorAccessor
{
    public string? GetUserId()
    {
        var principal = httpContextAccessor.HttpContext?.User;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = principal.FindFirst("sub")?.Value;
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException(
                "Kimliği doğrulanmış kullanıcının kullanıcı kimliği claim'i bulunamadı.");
        }

        return userId;
    }
}
