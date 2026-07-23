using BTBS420.RecruitmentSystem.Web.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class ApplicationDbContextTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ApplicationDbContextTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void ApplicationDbContext_ScopedSqlServerContextOlarakKaydedilir()
    {
        using var firstScope = _factory.Services.CreateScope();
        using var secondScope = _factory.Services.CreateScope();

        var firstContext = firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sameScopeContext = firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Same(firstContext, sameScopeContext);
        Assert.NotSame(firstContext, secondContext);
        Assert.True(firstContext.Database.IsSqlServer());

        var expectedConnection = new SqlConnectionStringBuilder(
            TestWebApplicationFactory.IsolatedConnectionString);
        var actualConnection = new SqlConnectionStringBuilder(
            firstContext.Database.GetConnectionString());

        Assert.Equal(expectedConnection.DataSource, actualConnection.DataSource);
        Assert.Equal(expectedConnection.InitialCatalog, actualConnection.InitialCatalog);
        Assert.Equal(expectedConnection.IntegratedSecurity, actualConnection.IntegratedSecurity);
        Assert.Equal(expectedConnection.Encrypt, actualConnection.Encrypt);
        Assert.Equal(
            expectedConnection.TrustServerCertificate,
            actualConnection.TrustServerCertificate);
    }

    [Fact]
    public void ApplicationDbContext_BaglantiYapilandirilmadiysaAciklayiciHataVerir()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = string.Empty
                    });
            });
        });
        using var scope = factory.Services.CreateScope();

        var exception = Assert.Throws<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());

        Assert.Contains("ConnectionStrings__DefaultConnection", exception.Message);
    }

    [Fact]
    public void ApplicationDbContext_InitialCreateMigrationiniIcerir()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Contains(
            context.Database.GetMigrations(),
            migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
    }

    [Fact]
    public void TestHostu_GelistiriciSqlServerYapilandirmasiniKullanmaz()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var connection = new SqlConnectionStringBuilder(
            context.Database.GetConnectionString());

        Assert.Equal("sql.test.invalid", connection.DataSource);
        Assert.Equal("BTBS420_IsolatedTests", connection.InitialCatalog);
        Assert.True(connection.IntegratedSecurity);
        Assert.True(connection.TrustServerCertificate);
        Assert.Equal(1, connection.ConnectTimeout);
        Assert.True(string.IsNullOrEmpty(connection.Password));
    }
}
