using System.Net;
using System.Text.RegularExpressions;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class CandidateFilterCatalogSqlServerIntegrationTests :
    IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN36_TEST_SQLSERVER_CONNECTION_STRING";

    private readonly TestWebApplicationFactory _baseFactory;

    public CandidateFilterCatalogSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationTheory]
    [InlineData("/Skills")]
    [InlineData("/Educations")]
    [InlineData("/Languages")]
    [InlineData("/Locations")]
    public async Task Katalog_GecerliIsimIleOlusturulur(string basePath)
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var name = $"Kan36-{basePath.Trim('/')}-{runId}";
        using var client = CreateClient(factory);

        var response = await PostAsync(
            client,
            $"{basePath}/Create",
            new Dictionary<string, string> { ["Name"] = name });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [SqlServerIntegrationTheory]
    [InlineData("/Skills")]
    [InlineData("/Educations")]
    [InlineData("/Languages")]
    [InlineData("/Locations")]
    public async Task Katalog_AyniIsimIkinciKezReddedilir(string basePath)
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var name = $"Kan36-Dup-{basePath.Trim('/')}-{runId}";
        using var firstClient = CreateClient(factory);
        using var secondClient = CreateClient(factory);

        await PostAsync(
            firstClient,
            $"{basePath}/Create",
            new Dictionary<string, string> { ["Name"] = name });
        var secondResponse = await PostAsync(
            secondClient,
            $"{basePath}/Create",
            new Dictionary<string, string> { ["Name"] = name });
        var body = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Contains("zaten kullan", body);
    }

    [SqlServerIntegrationTheory]
    [InlineData("/Skills")]
    [InlineData("/Educations")]
    [InlineData("/Languages")]
    [InlineData("/Locations")]
    public async Task Katalog_DeactivateVeActivateCalisir(string basePath)
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var name = $"Kan36-Toggle-{basePath.Trim('/')}-{runId}";
        using var createClient = CreateClient(factory);
        await PostAsync(
            createClient,
            $"{basePath}/Create",
            new Dictionary<string, string> { ["Name"] = name });

        var id = await GetEntityIdByNameAsync(basePath, name);

        using var deactivateClient = CreateClient(factory);
        var deactivateResponse = await PostAsync(
            deactivateClient,
            $"{basePath}/Deactivate/{id}",
            []);
        Assert.Equal(HttpStatusCode.Redirect, deactivateResponse.StatusCode);
        Assert.False(await IsActiveAsync(basePath, id));

        using var activateClient = CreateClient(factory);
        var activateResponse = await PostAsync(
            activateClient,
            $"{basePath}/Activate/{id}",
            []);
        Assert.Equal(HttpStatusCode.Redirect, activateResponse.StatusCode);
        Assert.True(await IsActiveAsync(basePath, id));
    }

    [SqlServerIntegrationFact]
    public async Task ExperienceRange_GecerliAralikIleOlusturulur()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var name = $"Kan36-ER-{runId}";
        var baseYear = Random.Shared.Next(10000, 99999);
        using var client = CreateClient(factory);

        var response = await PostAsync(
            client,
            "/ExperienceRanges/Create",
            new Dictionary<string, string>
            {
                ["Name"] = name,
                ["MinYears"] = baseYear.ToString(),
                ["MaxYears"] = (baseYear + 2).ToString()
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task ExperienceRange_MinBuyukMaxReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        using var client = CreateClient(factory);

        var response = await PostAsync(
            client,
            "/ExperienceRanges/Create",
            new Dictionary<string, string>
            {
                ["Name"] = $"Kan36-ER-Invalid-{runId}",
                ["MinYears"] = "10",
                ["MaxYears"] = "5"
            });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("minimumdan", body);
    }

    [SqlServerIntegrationFact]
    public async Task ExperienceRange_CakisanAralikReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var baseYear = Random.Shared.Next(10000, 99999);
        using var firstClient = CreateClient(factory);
        using var secondClient = CreateClient(factory);

        await PostAsync(
            firstClient,
            "/ExperienceRanges/Create",
            new Dictionary<string, string>
            {
                ["Name"] = $"Kan36-ER-First-{runId}",
                ["MinYears"] = baseYear.ToString(),
                ["MaxYears"] = (baseYear + 3).ToString()
            });

        var overlapResponse = await PostAsync(
            secondClient,
            "/ExperienceRanges/Create",
            new Dictionary<string, string>
            {
                ["Name"] = $"Kan36-ER-Overlap-{runId}",
                ["MinYears"] = (baseYear + 2).ToString(),
                ["MaxYears"] = (baseYear + 5).ToString()
            });
        var body = await overlapResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, overlapResponse.StatusCode);
        Assert.Contains("mevcut aktif", body);
    }

    [SqlServerIntegrationFact]
    public async Task ExperienceRange_CakismayanAralikKabulEdilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var baseYear = Random.Shared.Next(10000, 99999);
        using var firstClient = CreateClient(factory);
        using var secondClient = CreateClient(factory);

        await PostAsync(
            firstClient,
            "/ExperienceRanges/Create",
            new Dictionary<string, string>
            {
                ["Name"] = $"Kan36-ER-NoOverlapFirst-{runId}",
                ["MinYears"] = baseYear.ToString(),
                ["MaxYears"] = (baseYear + 2).ToString()
            });

        var response = await PostAsync(
            secondClient,
            "/ExperienceRanges/Create",
            new Dictionary<string, string>
            {
                ["Name"] = $"Kan36-ER-NoOverlapSecond-{runId}",
                ["MinYears"] = (baseYear + 3).ToString(),
                ["MaxYears"] = (baseYear + 5).ToString()
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task ExperienceRange_PasifAralikYeniAraligiEngellemez()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var firstName = $"Kan36-ER-Inactive-{runId}";
        var baseYear = Random.Shared.Next(10000, 99999);
        using var createClient = CreateClient(factory);
        await PostAsync(
            createClient,
            "/ExperienceRanges/Create",
            new Dictionary<string, string>
            {
                ["Name"] = firstName,
                ["MinYears"] = baseYear.ToString(),
                ["MaxYears"] = (baseYear + 2).ToString()
            });

        var firstId = await GetEntityIdByNameAsync("/ExperienceRanges", firstName);
        using var deactivateClient = CreateClient(factory);
        await PostAsync(deactivateClient, $"/ExperienceRanges/Deactivate/{firstId}", []);

        using var secondClient = CreateClient(factory);
        var response = await PostAsync(
            secondClient,
            "/ExperienceRanges/Create",
            new Dictionary<string, string>
            {
                ["Name"] = $"Kan36-ER-ReuseRange-{runId}",
                ["MinYears"] = baseYear.ToString(),
                ["MaxYears"] = (baseYear + 2).ToString()
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateSqlFactory()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            ConnectionStringEnvironmentVariable)!;

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

    private static async Task<int> GetEntityIdByNameAsync(string basePath, string name)
    {
        await using var context = CreateRawContext();

        return basePath switch
        {
            "/Skills" => (await context.Skills.SingleAsync(x => x.Name == name)).Id,
            "/Educations" => (await context.Educations.SingleAsync(x => x.Name == name)).Id,
            "/Languages" => (await context.Languages.SingleAsync(x => x.Name == name)).Id,
            "/Locations" => (await context.Locations.SingleAsync(x => x.Name == name)).Id,
            "/ExperienceRanges" =>
                (await context.ExperienceRanges.SingleAsync(x => x.Name == name)).Id,
            _ => throw new NotSupportedException(basePath)
        };
    }

    private static async Task<bool> IsActiveAsync(string basePath, int id)
    {
        await using var context = CreateRawContext();

        return basePath switch
        {
            "/Skills" => (await context.Skills.FindAsync(id))!.IsActive,
            "/Educations" => (await context.Educations.FindAsync(id))!.IsActive,
            "/Languages" => (await context.Languages.FindAsync(id))!.IsActive,
            "/Locations" => (await context.Locations.FindAsync(id))!.IsActive,
            "/ExperienceRanges" => (await context.ExperienceRanges.FindAsync(id))!.IsActive,
            _ => throw new NotSupportedException(basePath)
        };
    }

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string path,
        Dictionary<string, string> formFields)
    {
        var token = await GetAntiforgeryTokenAsync(client, GetFormUrl(path));
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
        formFields["__RequestVerificationToken"] = token;
        request.Content = new FormUrlEncodedContent(formFields);

        return await client.SendAsync(request);
    }

    private static string GetFormUrl(string postPath)
    {
        if (postPath.Contains("/Create"))
        {
            return postPath;
        }

        var controllerSegment = postPath.Split('/', StringSplitOptions.RemoveEmptyEntries)[0];

        return $"/{controllerSegment}";
    }

    private static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);

        var response = await client.SendAsync(request);
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

    private static bool IsEnvironmentVariableMissing()
    {
        return string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable));
    }

    private sealed class SqlServerIntegrationFactAttribute : FactAttribute
    {
        public SqlServerIntegrationFactAttribute()
        {
            if (IsEnvironmentVariableMissing())
            {
                Skip =
                    $"{ConnectionStringEnvironmentVariable} ayarlanmadığı için " +
                    "geçici SQL Server katalog entegrasyon testi atlandı.";
            }
        }
    }

    private sealed class SqlServerIntegrationTheoryAttribute : TheoryAttribute
    {
        public SqlServerIntegrationTheoryAttribute()
        {
            if (IsEnvironmentVariableMissing())
            {
                Skip =
                    $"{ConnectionStringEnvironmentVariable} ayarlanmadığı için " +
                    "geçici SQL Server katalog entegrasyon testi atlandı.";
            }
        }
    }
}
