using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class UsersController(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? role,
        int? departmentId,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(
                user =>
                    (user.UserName != null && user.UserName.Contains(trimmed)) ||
                    (user.Email != null && user.Email.Contains(trimmed)));
        }

        if (departmentId.HasValue)
        {
            query = query.Where(user => user.DepartmentId == departmentId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(user => user.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var roleId = await dbContext.Roles
                .Where(r => r.Name == role)
                .Select(r => r.Id)
                .SingleOrDefaultAsync(cancellationToken);

            var userIdsInRole = dbContext.UserRoles
                .Where(userRole => userRole.RoleId == roleId)
                .Select(userRole => userRole.UserId);

            query = query.Where(user => userIdsInRole.Contains(user.Id));
        }

        var matchedUsers = await query
            .OrderBy(user => user.UserName)
            .Select(
                user => new
                {
                    user.Id,
                    user.UserName,
                    user.Email,
                    user.IsActive,
                    DepartmentName = user.Department != null ? user.Department.Name : null
                })
            .ToListAsync(cancellationToken);

        var userIds = matchedUsers.Select(user => user.Id).ToList();
        var rolesByUserId = await (
                from userRole in dbContext.UserRoles
                join roleEntity in dbContext.Roles on userRole.RoleId equals roleEntity.Id
                where userIds.Contains(userRole.UserId)
                select new { userRole.UserId, RoleName = roleEntity.Name! })
            .ToListAsync(cancellationToken);

        var rolesLookup = rolesByUserId
            .GroupBy(entry => entry.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(entry => entry.RoleName)
                    .OrderBy(name => name)
                    .ToList());

        var items = matchedUsers
            .Select(
                user => new UserListItemViewModel(
                    user.Id,
                    user.UserName ?? string.Empty,
                    user.Email,
                    rolesLookup.TryGetValue(user.Id, out var roles) ? roles : [],
                    user.DepartmentName,
                    user.IsActive))
            .ToList();

        var departmentOptions = await dbContext.Departments
            .OrderBy(department => department.Name)
            .Select(department => new DepartmentOptionViewModel(department.Id, department.Name))
            .ToListAsync(cancellationToken);

        return View(
            new UserIndexViewModel(
                items,
                SystemRoles.All,
                departmentOptions,
                search,
                role,
                departmentId,
                isActive));
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(applicationUser => applicationUser.Department)
            .FirstOrDefaultAsync(applicationUser => applicationUser.Id == id, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        var roles = await userManager.GetRolesAsync(user);

        return View(
            new UserDetailsViewModel(
                user.Id,
                user.UserName ?? string.Empty,
                user.Email,
                roles.OrderBy(roleName => roleName).ToList(),
                user.Department?.Name,
                user.IsActive));
    }
}
