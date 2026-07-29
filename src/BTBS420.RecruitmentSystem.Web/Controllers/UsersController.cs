using System.Security.Cryptography;
using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class UsersController(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext,
    IActivityLogService activityLogService) : Controller
{
    internal const string GeneratedTemporaryPasswordTempDataKey =
        "GeneratedTemporaryPassword";

    internal const string UserActionErrorTempDataKey = "UserActionError";

    private const string DuplicateUserMessage =
        "Bu kullanıcı adı veya e-posta zaten kullanılıyor.";

    private const string OperationFailedMessage =
        "İşlem tamamlanamadı, lütfen tekrar deneyin.";

    private const string InvalidDepartmentMessage =
        "Seçilen departman geçersiz veya pasif.";

    private const string LastActiveAdminMessage =
        "Sistemdeki son aktif Admin pasifleştirilemez veya rolü değiştirilemez.";

    private static readonly IReadOnlyList<string> InternalRoles =
    [
        SystemRoles.Admin,
        SystemRoles.RecruitmentSpecialist,
        SystemRoles.HiringManager
    ];

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

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new UserFormViewModel
        {
            RoleOptions = InternalRoles,
            DepartmentOptions = await GetActiveDepartmentOptionsAsync(cancellationToken)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        UserFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!InternalRoles.Contains(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "Geçersiz rol seçimi.");
        }

        if (!ModelState.IsValid)
        {
            return await ReturnCreateViewWithOptionsAsync(model, cancellationToken);
        }

        var department = await dbContext.Departments.FindAsync(
            [model.DepartmentId!.Value],
            cancellationToken);

        if (department is null || !department.IsActive)
        {
            ModelState.AddModelError(nameof(model.DepartmentId), InvalidDepartmentMessage);
            return await ReturnCreateViewWithOptionsAsync(model, cancellationToken);
        }

        var user = new ApplicationUser
        {
            UserName = model.UserName.Trim(),
            Email = model.Email.Trim(),
            EmailConfirmed = true,
            DepartmentId = department.Id
        };
        var temporaryPassword = GenerateTemporaryPassword();

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        IdentityResult createResult;
        try
        {
            createResult = await userManager.CreateAsync(user, temporaryPassword);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            ModelState.AddModelError(string.Empty, DuplicateUserMessage);
            return await ReturnCreateViewWithOptionsAsync(model, cancellationToken);
        }

        if (!createResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);

            if (IsDuplicateError(createResult))
            {
                ModelState.AddModelError(string.Empty, DuplicateUserMessage);
            }
            else
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return await ReturnCreateViewWithOptionsAsync(model, cancellationToken);
        }

        IdentityResult roleResult;
        try
        {
            roleResult = await userManager.AddToRoleAsync(user, model.Role);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            ModelState.AddModelError(string.Empty, OperationFailedMessage);
            return await ReturnCreateViewWithOptionsAsync(model, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            ModelState.AddModelError(string.Empty, OperationFailedMessage);
            return await ReturnCreateViewWithOptionsAsync(model, cancellationToken);
        }

        if (!roleResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            ModelState.AddModelError(string.Empty, OperationFailedMessage);
            return await ReturnCreateViewWithOptionsAsync(model, cancellationToken);
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityCreated,
                "İç kullanıcı oluşturuldu.",
                ActivityEntityTypes.User,
                user.Id));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        TempData[GeneratedTemporaryPasswordTempDataKey] = temporaryPassword;
        return RedirectToAction(nameof(Details), new { id = user.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(applicationUser => applicationUser.Department)
            .FirstOrDefaultAsync(applicationUser => applicationUser.Id == id, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        var currentInternalRole = currentRoles.FirstOrDefault(InternalRoles.Contains);

        var model = new UserFormViewModel
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Role = currentInternalRole ?? string.Empty,
            DepartmentId = user.DepartmentId,
            RoleOptions = InternalRoles,
            DepartmentOptions = await GetActiveDepartmentOptionsAsync(
                cancellationToken,
                user.Department)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id,
        UserFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!InternalRoles.Contains(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "Geçersiz rol seçimi.");
        }

        var user = await dbContext.Users
            .Include(applicationUser => applicationUser.Department)
            .FirstOrDefaultAsync(applicationUser => applicationUser.Id == id, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            model.Id = user.Id;
            return await ReturnEditViewWithOptionsAsync(model, user.Department, cancellationToken);
        }

        var department = await dbContext.Departments.FindAsync(
            [model.DepartmentId!.Value],
            cancellationToken);

        if (department is null || !department.IsActive)
        {
            ModelState.AddModelError(nameof(model.DepartmentId), InvalidDepartmentMessage);
            model.Id = user.Id;
            return await ReturnEditViewWithOptionsAsync(model, user.Department, cancellationToken);
        }

        if (!string.Equals(model.Role, SystemRoles.Admin, StringComparison.Ordinal) &&
            await IsLastActiveAdminAsync(user, cancellationToken))
        {
            ModelState.AddModelError(string.Empty, LastActiveAdminMessage);
            model.Id = user.Id;
            return await ReturnEditViewWithOptionsAsync(model, user.Department, cancellationToken);
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        user.UserName = model.UserName.Trim();
        user.NormalizedUserName = userManager.NormalizeName(user.UserName);
        user.Email = model.Email.Trim();
        user.NormalizedEmail = userManager.NormalizeEmail(user.Email);
        user.DepartmentId = department.Id;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            ModelState.AddModelError(string.Empty, DuplicateUserMessage);
            model.Id = user.Id;
            return await ReturnEditViewWithOptionsAsync(model, user.Department, cancellationToken);
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles.Where(InternalRoles.Contains).ToList();

        try
        {
            if (rolesToRemove.Count > 0)
            {
                var removeResult = await userManager.RemoveFromRolesAsync(user, rolesToRemove);
                if (!removeResult.Succeeded)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    ModelState.AddModelError(string.Empty, OperationFailedMessage);
                    model.Id = user.Id;
                    return await ReturnEditViewWithOptionsAsync(
                        model,
                        user.Department,
                        cancellationToken);
                }
            }

            var addResult = await userManager.AddToRoleAsync(user, model.Role);
            if (!addResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                ModelState.AddModelError(string.Empty, OperationFailedMessage);
                model.Id = user.Id;
                return await ReturnEditViewWithOptionsAsync(
                    model,
                    user.Department,
                    cancellationToken);
            }
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            ModelState.AddModelError(string.Empty, OperationFailedMessage);
            model.Id = user.Id;
            return await ReturnEditViewWithOptionsAsync(model, user.Department, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            ModelState.AddModelError(string.Empty, OperationFailedMessage);
            model.Id = user.Id;
            return await ReturnEditViewWithOptionsAsync(model, user.Department, cancellationToken);
        }

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityUpdated,
                "İç kullanıcı güncellendi.",
                ActivityEntityTypes.User,
                user.Id));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { id = user.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(string id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FindAsync([id], cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        var roles = await userManager.GetRolesAsync(user);
        if (!roles.Any(InternalRoles.Contains))
        {
            return NotFound();
        }

        if (await IsLastActiveAdminAsync(user, cancellationToken))
        {
            TempData[UserActionErrorTempDataKey] = LastActiveAdminMessage;
            return RedirectToAction(nameof(Details), new { id = user.Id });
        }

        user.IsActive = false;

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "Kullanıcı pasife alındı.",
                ActivityEntityTypes.User,
                user.Id));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { id = user.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(string id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FindAsync([id], cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        var roles = await userManager.GetRolesAsync(user);
        if (!roles.Any(InternalRoles.Contains))
        {
            return NotFound();
        }

        user.IsActive = true;

        activityLogService.Stage(
            new ActivityLogEntry(
                ActivityActionCodes.EntityStatusChanged,
                "Kullanıcı aktifleştirildi.",
                ActivityEntityTypes.User,
                user.Id));
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { id = user.Id });
    }

    private async Task<bool> IsLastActiveAdminAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        if (!user.IsActive)
        {
            return false;
        }

        if (!await userManager.IsInRoleAsync(user, SystemRoles.Admin))
        {
            return false;
        }

        var adminRoleId = await dbContext.Roles
            .Where(role => role.Name == SystemRoles.Admin)
            .Select(role => role.Id)
            .SingleOrDefaultAsync(cancellationToken);

        var activeAdminCount = await (
                from userRole in dbContext.UserRoles
                join adminUser in dbContext.Users on userRole.UserId equals adminUser.Id
                where userRole.RoleId == adminRoleId && adminUser.IsActive
                select adminUser.Id)
            .CountAsync(cancellationToken);

        return activeAdminCount <= 1;
    }

    private async Task<IActionResult> ReturnCreateViewWithOptionsAsync(
        UserFormViewModel model,
        CancellationToken cancellationToken)
    {
        model.RoleOptions = InternalRoles;
        model.DepartmentOptions = await GetActiveDepartmentOptionsAsync(cancellationToken);
        return View(nameof(Create), model);
    }

    private async Task<IActionResult> ReturnEditViewWithOptionsAsync(
        UserFormViewModel model,
        Department? currentDepartment,
        CancellationToken cancellationToken)
    {
        model.RoleOptions = InternalRoles;
        model.DepartmentOptions = await GetActiveDepartmentOptionsAsync(
            cancellationToken,
            currentDepartment);
        return View(nameof(Edit), model);
    }

    private async Task<IReadOnlyList<DepartmentOptionViewModel>> GetActiveDepartmentOptionsAsync(
        CancellationToken cancellationToken,
        Department? includeEvenIfInactive = null)
    {
        var options = await dbContext.Departments
            .Where(department => department.IsActive)
            .OrderBy(department => department.Name)
            .Select(department => new DepartmentOptionViewModel(department.Id, department.Name))
            .ToListAsync(cancellationToken);

        if (includeEvenIfInactive is { IsActive: false } &&
            options.All(option => option.Id != includeEvenIfInactive.Id))
        {
            options.Add(
                new DepartmentOptionViewModel(
                    includeEvenIfInactive.Id,
                    includeEvenIfInactive.Name));
        }

        return options;
    }

    private static string GenerateTemporaryPassword()
    {
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string digits = "23456789";
        const string special = "!@#$%^&*?-_";
        const string all = lower + upper + digits + special;

        Span<char> buffer = stackalloc char[16];
        buffer[0] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        buffer[1] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        buffer[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        buffer[3] = special[RandomNumberGenerator.GetInt32(special.Length)];

        for (var i = 4; i < buffer.Length; i++)
        {
            buffer[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        for (var i = buffer.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
        }

        return new string(buffer);
    }

    private static bool IsDuplicateError(IdentityResult result)
    {
        return result.Errors.Any(
            error => error.Code is "DuplicateUserName" or "DuplicateEmail");
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException &&
            sqlException.Number is 2601 or 2627;
    }
}
