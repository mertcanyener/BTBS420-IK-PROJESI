using Microsoft.AspNetCore.Identity;

namespace BTBS420.RecruitmentSystem.Web.Models;

public sealed class ApplicationUser : IdentityUser
{
    public bool IsActive { get; set; } = true;

    public int? DepartmentId { get; set; }

    public Department? Department { get; set; }
}
