namespace BTBS420.RecruitmentSystem.Web.Identity;

internal sealed class IdentityDataSeeder(
    IdentityRoleSeeder roleSeeder,
    InitialAdminSeeder initialAdminSeeder,
    SampleRecruiterSeeder sampleRecruiterSeeder,
    LookupDataSeeder lookupDataSeeder) : IIdentityDataSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await roleSeeder.SeedAsync(cancellationToken);
        await initialAdminSeeder.SeedAsync(cancellationToken);
        await sampleRecruiterSeeder.SeedAsync(cancellationToken);
        await lookupDataSeeder.SeedAsync(cancellationToken);
    }
}
