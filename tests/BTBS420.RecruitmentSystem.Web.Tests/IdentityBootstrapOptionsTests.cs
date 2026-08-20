using BTBS420.RecruitmentSystem.Web.Identity;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class IdentityBootstrapOptionsTests
{
    [Fact]
    public void VarsayilanYapilandirma_IlkAdminOlusturmayiKapaliTutar()
    {
        var options = new IdentityBootstrapOptions();

        options.Validate();

        Assert.False(options.Enabled);
        Assert.Null(options.AdminEmail);
        Assert.Null(options.AdminPassword);
    }

    [Theory]
    [InlineData(null, "runtime-generated")]
    [InlineData("admin@example.test", null)]
    public void EtkinYapilandirma_EksikBilgiyiReddeder(
        string? email,
        string? password)
    {
        var options = new IdentityBootstrapOptions
        {
            Enabled = true,
            AdminEmail = email,
            AdminPassword = password
        };

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains(
            IdentityBootstrapOptions.AdminEmailEnvironmentVariable,
            exception.Message);
        Assert.Contains(
            IdentityBootstrapOptions.AdminPasswordEnvironmentVariable,
            exception.Message);
    }

    [Fact]
    public void EtkinYapilandirma_GuvenliKaynaktanGelenTamBilgiyiKabulEder()
    {
        var options = new IdentityBootstrapOptions
        {
            Enabled = true,
            AdminEmail = "admin@example.test",
            AdminPassword = $"{Guid.NewGuid():N}aA1!"
        };

        options.Validate();
    }
}
