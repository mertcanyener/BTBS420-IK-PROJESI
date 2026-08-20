using BTBS420.RecruitmentSystem.Web.Models;

namespace BTBS420.RecruitmentSystem.Web.PasswordReset;

public sealed class NoOpPasswordResetSender : IPasswordResetSender
{
    public Task SendAsync(
        ApplicationUser user,
        string resetLink,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
