using BTBS420.RecruitmentSystem.Web.Models;

namespace BTBS420.RecruitmentSystem.Web.PasswordReset;

public interface IPasswordResetSender
{
    Task SendAsync(
        ApplicationUser user,
        string resetLink,
        CancellationToken cancellationToken = default);
}
