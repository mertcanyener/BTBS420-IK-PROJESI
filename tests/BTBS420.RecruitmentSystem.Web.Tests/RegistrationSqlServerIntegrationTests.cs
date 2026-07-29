using System.Net;
using System.Text.RegularExpressions;
using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class RegistrationSqlServerIntegrationTests :
    IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN32_TEST_SQLSERVER_CONNECTION_STRING";

    private const string TestPassword = "Kan32-Gecici-Sifre-1!";

    private readonly TestWebApplicationFactory _baseFactory;

    public RegistrationSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task Register_GecerliBilgilerleHesapOlusturulurVeYalnizCandidateRolundeOlur()
    {
        using var factory = CreateRealAuthenticationFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var userName = $"kan32-user-{runId}";
        var email = $"kan32-{runId}@example.test";
        using var client = CreateClient(factory);

        var response = await PostRegisterAsync(client, userName, email, TestPassword);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.OriginalString);

        using var scope = factory.Services.CreateScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(userName);
        Assert.NotNull(user);
        Assert.True(user.IsActive);

        var roles = await userManager.GetRolesAsync(user);
        Assert.Equal([SystemRoles.Candidate], roles);
        Assert.False(await userManager.IsInRoleAsync(user, SystemRoles.Admin));
        Assert.False(
            await userManager.IsInRoleAsync(user, SystemRoles.RecruitmentSpecialist));
        Assert.False(await userManager.IsInRoleAsync(user, SystemRoles.HiringManager));
    }

    [SqlServerIntegrationFact]
    public async Task Register_FormdanRoleAdminGonderilseDahiCandidateOlur()
    {
        using var factory = CreateRealAuthenticationFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var userName = $"kan32-forged-role-{runId}";
        var email = $"kan32-forged-role-{runId}@example.test";
        using var client = CreateClient(factory);

        var response = await PostRegisterAsync(
            client,
            userName,
            email,
            TestPassword,
            extraFormFields: new Dictionary<string, string> { ["Role"] = "Admin" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(userName);
        Assert.NotNull(user);
        var roles = await userManager.GetRolesAsync(user);
        Assert.Equal([SystemRoles.Candidate], roles);
    }

    [SqlServerIntegrationFact]
    public async Task Register_FormdanRoleIdGonderilseDahiYetkiYukseltilemez()
    {
        using var factory = CreateRealAuthenticationFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var userName = $"kan32-forged-roleid-{runId}";
        var email = $"kan32-forged-roleid-{runId}@example.test";
        using var client = CreateClient(factory);

        var response = await PostRegisterAsync(
            client,
            userName,
            email,
            TestPassword,
            extraFormFields: new Dictionary<string, string>
            {
                ["RoleId"] = "1",
                ["Roles"] = "Admin,İşe Alım Uzmanı"
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(userName);
        Assert.NotNull(user);
        var roles = await userManager.GetRolesAsync(user);
        Assert.Equal([SystemRoles.Candidate], roles);
    }

    [SqlServerIntegrationFact]
    public async Task Register_IsAdminGibiEkstraAlanlarEtkisizdir()
    {
        using var factory = CreateRealAuthenticationFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var userName = $"kan32-forged-flags-{runId}";
        var email = $"kan32-forged-flags-{runId}@example.test";
        using var client = CreateClient(factory);

        var response = await PostRegisterAsync(
            client,
            userName,
            email,
            TestPassword,
            extraFormFields: new Dictionary<string, string>
            {
                ["IsAdmin"] = "true",
                ["IsRecruiter"] = "true",
                ["IsManager"] = "true"
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(userName);
        Assert.NotNull(user);
        var roles = await userManager.GetRolesAsync(user);
        Assert.Equal([SystemRoles.Candidate], roles);
    }

    [SqlServerIntegrationFact]
    public async Task Register_AyniUsernameIkinciKezKullanilamaz()
    {
        using var factory = CreateRealAuthenticationFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var userName = $"kan32-dup-username-{runId}";
        using var firstClient = CreateClient(factory);
        using var secondClient = CreateClient(factory);

        var firstResponse = await PostRegisterAsync(
            firstClient,
            userName,
            $"kan32-first-{runId}@example.test",
            TestPassword);
        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);

        var secondResponse = await PostRegisterAsync(
            secondClient,
            userName,
            $"kan32-second-{runId}@example.test",
            TestPassword);
        var body = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Contains("zaten kullan", body);
    }

    [SqlServerIntegrationFact]
    public async Task Register_AyniEmailIkinciKezKullanilamaz()
    {
        using var factory = CreateRealAuthenticationFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var email = $"kan32-dup-email-{runId}@example.test";
        using var firstClient = CreateClient(factory);
        using var secondClient = CreateClient(factory);

        var firstResponse = await PostRegisterAsync(
            firstClient,
            $"kan32-first-{runId}",
            email,
            TestPassword);
        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);

        var secondResponse = await PostRegisterAsync(
            secondClient,
            $"kan32-second-{runId}",
            email,
            TestPassword);
        var body = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Contains("zaten kullan", body);
    }

    [SqlServerIntegrationFact]
    public async Task Register_UsernameCasingDuplicateEngellenir()
    {
        using var factory = CreateRealAuthenticationFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var userName = $"kan32-casing-{runId}";
        using var firstClient = CreateClient(factory);
        using var secondClient = CreateClient(factory);

        var firstResponse = await PostRegisterAsync(
            firstClient,
            userName,
            $"kan32-casing-first-{runId}@example.test",
            TestPassword);
        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);

        var secondResponse = await PostRegisterAsync(
            secondClient,
            userName.ToUpperInvariant(),
            $"kan32-casing-second-{runId}@example.test",
            TestPassword);
        var body = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Contains("zaten kullan", body);
    }

    [SqlServerIntegrationFact]
    public async Task Register_EmailCasingDuplicateEngellenir()
    {
        using var factory = CreateRealAuthenticationFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var email = $"kan32-emailcasing-{runId}@example.test";
        using var firstClient = CreateClient(factory);
        using var secondClient = CreateClient(factory);

        var firstResponse = await PostRegisterAsync(
            firstClient,
            $"kan32-emailcasing-first-{runId}",
            email,
            TestPassword);
        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);

        var secondResponse = await PostRegisterAsync(
            secondClient,
            $"kan32-emailcasing-second-{runId}",
            email.ToUpperInvariant(),
            TestPassword);
        var body = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Contains("zaten kullan", body);
    }

    [SqlServerIntegrationFact]
    public async Task Register_ZayifParolaPasswordPolicyTarafindanReddedilir()
    {
        using var factory = CreateRealAuthenticationFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        using var client = CreateClient(factory);

        var response = await PostRegisterAsync(
            client,
            $"kan32-weak-{runId}",
            $"kan32-weak-{runId}@example.test",
            "zayif");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("zayif", body);
    }

    [SqlServerIntegrationFact]
    public async Task Register_RolAtamaBasarisizOlursaYarimKullaniciKalmaz()
    {
        // Paylaşılan BTBS420_KAN32_Test veritabanı diğer testler tarafından
        // Aday rolüyle seed edilmiş olabileceğinden (ve bu paylaşılan
        // veritabanından rol silmek diğer testleri etkileyeceğinden), bu
        // test kendi izole, rol-seed edilmemiş geçici veritabanını kullanır.
        var databaseName = await CreateIsolatedDatabaseAsync();

        try
        {
            var connectionString = BuildConnectionString(databaseName);
            using var factory = CreateRealAuthenticationFactory(connectionString);

            using (var roleScope = factory.Services.CreateScope())
            {
                var roleManager =
                    roleScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                Assert.False(await roleManager.RoleExistsAsync(SystemRoles.Candidate));
            }

            var runId = Guid.NewGuid().ToString("N");
            var userName = $"kan32-norole-{runId}";
            using var client = CreateClient(factory);

            var response = await PostRegisterAsync(
                client,
                userName,
                $"kan32-norole-{runId}@example.test",
                TestPassword);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("tamamlanamad", body);

            using var verifyScope = factory.Services.CreateScope();
            var userManager =
                verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByNameAsync(userName);
            Assert.Null(user);
        }
        finally
        {
            await DropIsolatedDatabaseAsync(databaseName);
        }
    }

    [SqlServerIntegrationFact]
    public async Task Register_ParolaResponseVeAuditaSizmaz()
    {
        using var factory = CreateRealAuthenticationFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var userName = $"kan32-noleak-{runId}";
        using var client = CreateClient(factory);

        var response = await PostRegisterAsync(
            client,
            userName,
            $"kan32-noleak-{runId}@example.test",
            TestPassword);
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(TestPassword, body);

        var userId = await GetUserIdAsync(factory, userName);
        await using var context = CreateRawContext();
        var registeredLog = await context.ActivityLogs
            .Where(
                log =>
                    log.ActionCode == ActivityActionCodes.UserRegistered &&
                    log.TargetEntityId == userId)
            .FirstOrDefaultAsync();

        Assert.NotNull(registeredLog);
        Assert.Equal(ActivityEntityTypes.User, registeredLog.TargetEntityType);
        Assert.DoesNotContain(TestPassword, registeredLog.Summary);
        Assert.DoesNotContain(userName, registeredLog.Summary);
    }

    [SqlServerIntegrationFact]
    public async Task Register_BasariliKayitLoginSayfasinaYonlendirir()
    {
        using var factory = CreateRealAuthenticationFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        using var client = CreateClient(factory);

        var response = await PostRegisterAsync(
            client,
            $"kan32-redirect-{runId}",
            $"kan32-redirect-{runId}@example.test",
            TestPassword);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.OriginalString);

        var loginPage = await client.GetAsync("/Account/Login");
        var loginBody = await loginPage.Content.ReadAsStringAsync();
        Assert.Contains("alert-success", loginBody);
    }

    [SqlServerIntegrationFact]
    public async Task Register_NormalizedEmailIcinDatabaseUniqueIndexDogrudanIkinciKaydiReddeder()
    {
        using var factory = CreateRealAuthenticationFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var email = $"kan32-dbunique-{runId}@example.test";
        var normalizedEmail = email.ToUpperInvariant();

        await using var context = CreateRawContext();
        context.Users.Add(
            new ApplicationUser
            {
                UserName = $"kan32-dbunique-first-{runId}",
                NormalizedUserName = $"kan32-dbunique-first-{runId}".ToUpperInvariant(),
                Email = email,
                NormalizedEmail = normalizedEmail
            });
        await context.SaveChangesAsync();

        context.Users.Add(
            new ApplicationUser
            {
                UserName = $"kan32-dbunique-second-{runId}",
                NormalizedUserName = $"kan32-dbunique-second-{runId}".ToUpperInvariant(),
                Email = email,
                NormalizedEmail = normalizedEmail
            });

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
    }

    [SqlServerIntegrationFact]
    public async Task Register_EszamanliDuplicateRegistrationYalnizBirBasariliKayitUretir()
    {
        using var factory = CreateRealAuthenticationFactory();
        await EnsureRolesSeededAsync(factory);
        var runId = Guid.NewGuid().ToString("N");
        var email = $"kan32-race-{runId}@example.test";
        using var firstClient = CreateClient(factory);
        using var secondClient = CreateClient(factory);

        var results = await Task.WhenAll(
            PostRegisterAsync(
                firstClient,
                $"kan32-race-first-{runId}",
                email,
                TestPassword),
            PostRegisterAsync(
                secondClient,
                $"kan32-race-second-{runId}",
                email,
                TestPassword));

        var successCount = results.Count(
            response => response.StatusCode == HttpStatusCode.Redirect);

        Assert.Equal(1, successCount);
    }

    private WebApplicationFactory<Program> CreateRealAuthenticationFactory(
        string? connectionStringOverride = null)
    {
        var connectionString = connectionStringOverride
            ?? Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)!;

        return _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = connectionString
                    });
            });
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme =
                        IdentityConstants.ApplicationScheme;
                    options.DefaultChallengeScheme =
                        IdentityConstants.ApplicationScheme;
                    options.DefaultSignInScheme =
                        IdentityConstants.ApplicationScheme;
                    options.DefaultForbidScheme =
                        IdentityConstants.ApplicationScheme;
                });
            });
        });
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
    {
        return factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true
            });
    }

    private static string BuildConnectionString(string databaseName)
    {
        var baseConnectionString = Environment.GetEnvironmentVariable(
            ConnectionStringEnvironmentVariable)!;
        var builder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = databaseName
        };

        return builder.ConnectionString;
    }

    private static async Task<string> CreateIsolatedDatabaseAsync()
    {
        var databaseName = $"BTBS420_KAN32_RoleRollback_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(BuildConnectionString(databaseName))
            .Options;

        await using var context = new ApplicationDbContext(options);
        await context.Database.MigrateAsync();

        return databaseName;
    }

    private static async Task DropIsolatedDatabaseAsync(string databaseName)
    {
        SqlConnection.ClearAllPools();

        await using var connection = new SqlConnection(BuildConnectionString("master"));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"DROP DATABASE IF EXISTS [{databaseName}];";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task EnsureRolesSeededAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var roleManager =
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var roleName in SystemRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }
    }

    private static async Task<string> GetUserIdAsync(
        WebApplicationFactory<Program> factory,
        string userName)
    {
        using var scope = factory.Services.CreateScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(userName)
            ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

        return user.Id;
    }

    private static async Task<HttpResponseMessage> PostRegisterAsync(
        HttpClient client,
        string userName,
        string email,
        string password,
        IReadOnlyDictionary<string, string>? extraFormFields = null)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Register");
        var formValues = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["UserName"] = userName,
            ["Email"] = email,
            ["Password"] = password,
            ["ConfirmPassword"] = password
        };

        if (extraFormFields is not null)
        {
            foreach (var (key, value) in extraFormFields)
            {
                formValues[key] = value;
            }
        }

        using var content = new FormUrlEncodedContent(formValues);

        return await client.PostAsync("/Account/Register", content);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        string url)
    {
        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        var tokenMatch = Regex.Match(
            content,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(
            tokenMatch.Success,
            $"Antiforgery form alanı bulunamadı ({url}).");

        return WebUtility.HtmlDecode(tokenMatch.Groups[1].Value);
    }

    private static ApplicationDbContext CreateRawContext()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            ConnectionStringEnvironmentVariable)!;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class SqlServerIntegrationFactAttribute : FactAttribute
    {
        public SqlServerIntegrationFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable(
                        ConnectionStringEnvironmentVariable)))
            {
                Skip =
                    $"{ConnectionStringEnvironmentVariable} ayarlanmadığı için " +
                    "geçici SQL Server kayıt entegrasyon testi atlandı.";
            }
        }
    }
}
