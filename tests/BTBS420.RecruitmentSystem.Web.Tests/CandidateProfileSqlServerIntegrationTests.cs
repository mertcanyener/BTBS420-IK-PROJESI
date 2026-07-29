using System.Net;
using System.Text.RegularExpressions;
using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class CandidateProfileSqlServerIntegrationTests :
    IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN45_TEST_SQLSERVER_CONNECTION_STRING";

    private readonly TestWebApplicationFactory _baseFactory;

    public CandidateProfileSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task Index_GecerliBilgilerleProfilOlusurVeAuditKaydeder()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var skillId = await CreateSkillAsync(factory, $"Kan45-Skill-{runId}");
        var languageId = await CreateLanguageAsync(factory, $"Kan45-Lang-{runId}");
        var candidateId = $"kan45-candidate-{runId}";
        using var client = CreateClient(factory);

        var response = await PostAsync(
            client,
            candidateId,
            new Dictionary<string, string>
            {
                ["FirstName"] = "Ayşe",
                ["LastName"] = "Yılmaz",
                ["ProfessionalSummary"] = "Backend geliştirme deneyimi.",
                ["SelectedSkillIds"] = skillId.ToString(),
                ["SelectedLanguageIds"] = languageId.ToString()
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var profile = await context.CandidateProfiles
            .SingleOrDefaultAsync(p => p.ApplicationUserId == candidateId);
        Assert.NotNull(profile);
        Assert.Equal("Ayşe", profile.FirstName);

        var skillLink = await context.CandidateProfileSkills
            .SingleOrDefaultAsync(l => l.CandidateProfileId == profile.Id && l.SkillId == skillId);
        Assert.NotNull(skillLink);

        var languageLink = await context.CandidateProfileLanguages
            .SingleOrDefaultAsync(l => l.CandidateProfileId == profile.Id && l.LanguageId == languageId);
        Assert.NotNull(languageLink);

        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityCreated &&
                    l.TargetEntityType == ActivityEntityTypes.Candidate &&
                    l.TargetEntityId == profile.Id.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(log);
        Assert.Equal(candidateId, log.CandidateId);
    }

    [SqlServerIntegrationFact]
    public async Task Index_AyniYetkinlikTekrarGonderilirseTekKayitOlusur()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var skillId = await CreateSkillAsync(factory, $"Kan45-DupSkill-{runId}");
        var candidateId = $"kan45-dup-candidate-{runId}";
        using var client = CreateClient(factory);
        var token = await GetAntiforgeryTokenAsync(client, candidateId);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/CandidateProfile");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateId);
        request.Content = new FormUrlEncodedContent(
            new[]
            {
                new KeyValuePair<string, string>("FirstName", "Mert"),
                new KeyValuePair<string, string>("LastName", "Can"),
                new KeyValuePair<string, string>("SelectedSkillIds", skillId.ToString()),
                new KeyValuePair<string, string>("SelectedSkillIds", skillId.ToString()),
                new KeyValuePair<string, string>("__RequestVerificationToken", token)
            });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var profile = await context.CandidateProfiles
            .SingleAsync(p => p.ApplicationUserId == candidateId);
        var linkCount = await context.CandidateProfileSkills
            .CountAsync(l => l.CandidateProfileId == profile.Id && l.SkillId == skillId);
        Assert.Equal(1, linkCount);
    }

    [SqlServerIntegrationFact]
    public async Task Index_BaskaAdayinProfiliniGoremezVeDuzenleyemez()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var candidateAId = $"kan45-a-{runId}";
        var candidateBId = $"kan45-b-{runId}";
        using var client = CreateClient(factory);

        await PostAsync(
            client,
            candidateAId,
            new Dictionary<string, string>
            {
                ["FirstName"] = "AdayA",
                ["LastName"] = "Soyad"
            });

        using var bClient = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/CandidateProfile");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateBId);

        var response = await bClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("AdayA", body);

        await using var context = CreateRawContext();
        var profileCountForB = await context.CandidateProfiles
            .CountAsync(p => p.ApplicationUserId == candidateBId);
        Assert.Equal(0, profileCountForB);
    }

    [SqlServerIntegrationFact]
    public async Task Index_IkinciKayitMevcutProfiliGunceller()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var candidateId = $"kan45-update-{runId}";
        using var client = CreateClient(factory);

        await PostAsync(
            client,
            candidateId,
            new Dictionary<string, string>
            {
                ["FirstName"] = "İlk",
                ["LastName"] = "Kayıt"
            });

        var response = await PostAsync(
            client,
            candidateId,
            new Dictionary<string, string>
            {
                ["FirstName"] = "Güncel",
                ["LastName"] = "Kayıt"
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var profileCount = await context.CandidateProfiles
            .CountAsync(p => p.ApplicationUserId == candidateId);
        Assert.Equal(1, profileCount);

        var profile = await context.CandidateProfiles
            .SingleAsync(p => p.ApplicationUserId == candidateId);
        Assert.Equal("Güncel", profile.FirstName);
    }

    private static async Task<int> CreateSkillAsync(
        WebApplicationFactory<Program> factory,
        string name)
    {
        using var client = CreateClient(factory);
        var token = await GetAntiforgeryTokenAsync(client, "/Skills/Create", SystemRoles.Admin);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Skills/Create");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Name"] = name,
                ["__RequestVerificationToken"] = token
            });
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var skill = await context.Skills.SingleAsync(s => s.Name == name);

        return skill.Id;
    }

    private static async Task<int> CreateLanguageAsync(
        WebApplicationFactory<Program> factory,
        string name)
    {
        using var client = CreateClient(factory);
        var token = await GetAntiforgeryTokenAsync(client, "/Languages/Create", SystemRoles.Admin);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Languages/Create");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Admin);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Name"] = name,
                ["__RequestVerificationToken"] = token
            });
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var language = await context.Languages.SingleAsync(l => l.Name == name);

        return language.Id;
    }

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string candidateId,
        Dictionary<string, string> formFields)
    {
        var token = await GetAntiforgeryTokenAsync(client, candidateId);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/CandidateProfile");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateId);
        formFields["__RequestVerificationToken"] = token;
        request.Content = new FormUrlEncodedContent(formFields);

        return await client.SendAsync(request);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        string candidateId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/CandidateProfile");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateId);

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        return ExtractAntiforgeryToken(content, "/CandidateProfile");
    }

    private static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        string url,
        string role)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, role);

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        return ExtractAntiforgeryToken(content, url);
    }

    private static string ExtractAntiforgeryToken(string content, string url)
    {
        var tokenMatch = Regex.Match(
            content,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(tokenMatch.Success, $"Antiforgery form alanı bulunamadı ({url}).");

        return WebUtility.HtmlDecode(tokenMatch.Groups[1].Value);
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
                    "geçici SQL Server aday profili entegrasyon testi atlandı.";
            }
        }
    }
}
