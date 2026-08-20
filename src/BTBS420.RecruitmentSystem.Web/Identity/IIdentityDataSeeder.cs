namespace BTBS420.RecruitmentSystem.Web.Identity;

public interface IIdentityDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
