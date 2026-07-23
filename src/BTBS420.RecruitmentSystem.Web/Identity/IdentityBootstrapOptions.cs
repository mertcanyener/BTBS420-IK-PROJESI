namespace BTBS420.RecruitmentSystem.Web.Identity;

public sealed class IdentityBootstrapOptions
{
    public const string SectionName = "IdentityBootstrap";
    public const string EnabledEnvironmentVariable = "IdentityBootstrap__Enabled";
    public const string AdminEmailEnvironmentVariable = "IdentityBootstrap__AdminEmail";
    public const string AdminPasswordEnvironmentVariable = "IdentityBootstrap__AdminPassword";

    public bool Enabled { get; set; }

    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(AdminEmail) ||
            string.IsNullOrWhiteSpace(AdminPassword))
        {
            throw new InvalidOperationException(
                "İlk Admin oluşturma etkinleştirildiğinde e-posta ve parola " +
                "güvenli yapılandırmadan sağlanmalıdır. " +
                $"{AdminEmailEnvironmentVariable} ve " +
                $"{AdminPasswordEnvironmentVariable} ortam değişkenlerini " +
                "veya eşdeğer user secrets değerlerini ayarlayın.");
        }
    }
}
