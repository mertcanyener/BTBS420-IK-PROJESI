namespace BTBS420.RecruitmentSystem.Web.Identity;

internal sealed class IdentityDataSeeder(
    IdentityRoleSeeder roleSeeder,
    InitialAdminSeeder initialAdminSeeder,
    SampleRecruiterSeeder sampleRecruiterSeeder,
    SampleHiringManagerSeeder sampleHiringManagerSeeder,
    LookupDataSeeder lookupDataSeeder,
    SampleCandidateSeeder sampleCandidateSeeder) : IIdentityDataSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await roleSeeder.SeedAsync(cancellationToken);
        await initialAdminSeeder.SeedAsync(cancellationToken);
        await sampleRecruiterSeeder.SeedAsync(cancellationToken);
        await sampleHiringManagerSeeder.SeedAsync(cancellationToken);
        await lookupDataSeeder.SeedAsync(cancellationToken);
        await sampleCandidateSeeder.SeedAsync(cancellationToken);
    }
}
