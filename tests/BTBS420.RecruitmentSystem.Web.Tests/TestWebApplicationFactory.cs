using BTBS420.RecruitmentSystem.Web.Identity;
using BTBS420.RecruitmentSystem.Web.Notifications;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
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
    private readonly Func<IServiceProvider, INotificationCenterService>?
        _notificationCenterServiceFactory;

    internal const string EnvironmentName = "Testing";
    internal const string IsolatedConnectionString =
        "Server=sql.test.invalid;Database=BTBS420_IsolatedTests;" +
        "Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=1";

    public TestWebApplicationFactory()
    {
    }

    internal TestWebApplicationFactory(
        Func<IServiceProvider, INotificationCenterService>
            notificationCenterServiceFactory)
    {
        _notificationCenterServiceFactory =
            notificationCenterServiceFactory ??
            throw new ArgumentNullException(
                nameof(notificationCenterServiceFactory));
    }

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
            services.RemoveAll<IDataProtectionProvider>();
            services.AddSingleton<IDataProtectionProvider>(
                new EphemeralDataProtectionProvider());
            services.RemoveAll<INotificationPublisher>();
            services.RemoveAll<INotificationCenterService>();
            services.AddSingleton<NoOpNotificationService>();
            services.AddSingleton<INotificationPublisher>(
                serviceProvider =>
                    serviceProvider.GetRequiredService<NoOpNotificationService>());
            services.AddSingleton<INotificationCenterService>(
                serviceProvider =>
                    serviceProvider.GetRequiredService<NoOpNotificationService>());

            if (_notificationCenterServiceFactory is not null)
            {
                services.RemoveAll<INotificationCenterService>();
                services.AddScoped(_notificationCenterServiceFactory);
            }

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

    private sealed class NoOpNotificationService :
        INotificationPublisher,
        INotificationCenterService
    {
        public Task<bool> StageIfMissingAsync(
            NotificationEntry entry,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<IReadOnlyList<NotificationListItem>> GetNotificationsAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<NotificationListItem>>([]);
        }

        public Task<int> GetUnreadCountAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public Task<bool> MarkAsReadAsync(
            long notificationId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<int> MarkAllAsReadAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }
    }
}
