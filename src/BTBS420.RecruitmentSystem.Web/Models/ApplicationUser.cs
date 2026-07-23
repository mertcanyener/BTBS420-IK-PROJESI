using Microsoft.AspNetCore.Identity;

namespace BTBS420.RecruitmentSystem.Web.Models;

public sealed class ApplicationUser : IdentityUser
{
    public bool IsActive { get; set; } = true;
}
