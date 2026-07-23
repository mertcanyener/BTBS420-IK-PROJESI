using BTBS420.RecruitmentSystem.Web.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    internal const string EnvironmentName = "Testing";
    internal const string IsolatedConnectionString =
        "Server=sql.test.invalid;Database=BTBS420_IsolatedTests;" +
        "Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=1";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseContentRoot(FindWebContentRoot());
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = IsolatedConnectionString,
                    ["IdentityBootstrap:Enabled"] = bool.FalseString,
                    ["IdentityBootstrap:AdminEmail"] = string.Empty,
                    ["IdentityBootstrap:AdminPassword"] = string.Empty
                });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IIdentityDataSeeder>();
            services.AddSingleton<IIdentityDataSeeder, NoOpIdentityDataSeeder>();

            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme =
                        TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme =
                        TestAuthenticationHandler.SchemeName;
                    options.DefaultForbidScheme =
                        TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });

            services
                .AddControllersWithViews()
                .AddApplicationPart(typeof(AuthorizationProbeController).Assembly);
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

    private sealed class NoOpIdentityDataSeeder : IIdentityDataSeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
