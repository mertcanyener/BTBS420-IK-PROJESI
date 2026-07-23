namespace BTBS420.RecruitmentSystem.Web.Identity;

internal sealed class IdentityDataSeeder(
    IdentityRoleSeeder roleSeeder,
    InitialAdminSeeder initialAdminSeeder) : IIdentityDataSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await roleSeeder.SeedAsync(cancellationToken);
        await initialAdminSeeder.SeedAsync(cancellationToken);
    }
}
