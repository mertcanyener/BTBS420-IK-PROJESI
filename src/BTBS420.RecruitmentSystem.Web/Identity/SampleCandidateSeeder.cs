using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BTBS420.RecruitmentSystem.Web.Identity;

public sealed class SampleCandidateSeeder(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext,
    IOptions<IdentityBootstrapOptions> bootstrapOptions)
{
    private const string Email = "aday@local.test";
    private const string Password = "Aday123!Strong";
    private const string FirstName = "Örnek";
    private const string LastName = "Aday";
    private const string ProfessionalSummary = "Yazılım geliştirme alanında deneyimli, örnek aday profili.";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!bootstrapOptions.Value.Enabled)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var existingUser = await userManager.FindByEmailAsync(Email);

        if (existingUser is not null)
        {
            if (!await userManager.IsInRoleAsync(existingUser, SystemRoles.Candidate))
            {
                var addRoleResult = await userManager.AddToRoleAsync(
                    existingUser,
                    SystemRoles.Candidate);
                EnsureSucceeded(addRoleResult, "Örnek Aday rolü atanamadı");
            }

            await EnsureProfileAsync(existingUser.Id, cancellationToken);
            return;
        }

        var candidateUser = new ApplicationUser
        {
            UserName = Email,
            Email = Email,
            EmailConfirmed = true,
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(candidateUser, Password);
        EnsureSucceeded(createResult, "Örnek Aday kullanıcısı oluşturulamadı");

        var roleResult = await userManager.AddToRoleAsync(
            candidateUser,
            SystemRoles.Candidate);
        EnsureSucceeded(roleResult, "Örnek Aday rolü atanamadı");

        await EnsureProfileAsync(candidateUser.Id, cancellationToken);
    }

    private async Task EnsureProfileAsync(string applicationUserId, CancellationToken cancellationToken)
    {
        var profileExists = await dbContext.CandidateProfiles
            .AnyAsync(profile => profile.ApplicationUserId == applicationUserId, cancellationToken);

        if (profileExists)
        {
            return;
        }

        var targetPosition = await dbContext.Positions
            .Where(position => position.IsActive)
            .OrderBy(position => position.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (targetPosition is null)
        {
            throw new InvalidOperationException(
                "Örnek Aday profili için hedef pozisyon bulunamadı. " +
                "LookupDataSeeder'ın bu seeder'dan önce çalıştığından emin olun.");
        }

        var profile = new CandidateProfile(
            applicationUserId,
            FirstName,
            LastName,
            ProfessionalSummary,
            targetPosition.Id);

        dbContext.CandidateProfiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errorCodes = string.Join(
            ", ",
            result.Errors.Select(error => error.Code));

        throw new InvalidOperationException(
            $"{operation}. Identity hata kodları: {errorCodes}");
    }
}
