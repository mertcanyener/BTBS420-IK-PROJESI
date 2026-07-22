using BTBS420.RecruitmentSystem.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class ApplicationDbContextTests
{
    private const string TestConnectionString =
        "Server=sql.test.invalid;Database=BTBS420Tests;" +
        "Integrated Security=True;Encrypt=True;TrustServerCertificate=True";

    [Fact]
    public void ApplicationDbContext_ScopedSqlServerContextOlarakKaydedilir()
    {
        using var factory = CreateFactory(TestConnectionString);
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();

        var firstContext = firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sameScopeContext = firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Same(firstContext, sameScopeContext);
        Assert.NotSame(firstContext, secondContext);
        Assert.True(firstContext.Database.IsSqlServer());

        var expectedConnection = new SqlConnectionStringBuilder(TestConnectionString);
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
        using var factory = CreateFactory(string.Empty);
        using var scope = factory.Services.CreateScope();

        var exception = Assert.Throws<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());

        Assert.Contains("ConnectionStrings__DefaultConnection", exception.Message);
    }

    [Fact]
    public void ApplicationDbContext_InitialCreateMigrationiniIcerir()
    {
        using var factory = CreateFactory(TestConnectionString);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Contains(
            context.Database.GetMigrations(),
            migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseContentRoot(FindWebContentRoot());
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:DefaultConnection"] = connectionString
                        });
                });
            });
    }

    private static string FindWebContentRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "BTBS420.RecruitmentSystem.Web");

            if (File.Exists(Path.Combine(candidate, "BTBS420.RecruitmentSystem.Web.csproj")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Web projesinin içerik kökü bulunamadı.");
    }
}
