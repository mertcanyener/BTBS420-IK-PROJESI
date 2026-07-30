using System.Net;
using System.Text.RegularExpressions;
using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class CandidateExperiencesSqlServerIntegrationTests :
    IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN46_TEST_SQLSERVER_CONNECTION_STRING";

    private readonly TestWebApplicationFactory _baseFactory;

    public CandidateExperiencesSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task Index_GecerliBilgilerleDeneyimOlusurVeAuditKaydeder()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var candidateId = $"kan46-exp-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var client = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(client, candidateId);

        var response = await PostAsync(
            client,
            candidateId,
            new Dictionary<string, string>
            {
                ["CompanyName"] = "Örnek A.Ş.",
                ["JobTitle"] = "Yazılım Geliştirici",
                ["StartDate"] = "2020-01-15",
                ["EndDate"] = "2023-05-31"
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var experience = await context.CandidateExperiences
            .SingleOrDefaultAsync(e => e.CandidateProfileId == profileId);
        Assert.NotNull(experience);
        Assert.Equal("Örnek A.Ş.", experience.CompanyName);
        Assert.Equal(new DateOnly(2023, 5, 31), experience.EndDate);

        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityCreated &&
                    l.TargetEntityType == ActivityEntityTypes.CandidateExperience &&
                    l.TargetEntityId == experience.Id.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(log);
        Assert.Equal(candidateId, log.CandidateId);
    }

    [SqlServerIntegrationFact]
    public async Task Create_DevamEdenIsBitisTarihiOlmadanKaydedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var candidateId = $"kan46-exp-ongoing-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var client = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(client, candidateId);

        var response = await PostAsync(
            client,
            candidateId,
            new Dictionary<string, string>
            {
                ["CompanyName"] = "Devam Eden Şirket",
                ["JobTitle"] = "Kıdemli Geliştirici",
                ["StartDate"] = "2023-01-01",
                ["IsOngoing"] = "true"
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var experience = await context.CandidateExperiences
            .SingleAsync(e => e.CandidateProfileId == profileId);
        Assert.Null(experience.EndDate);
    }

    [SqlServerIntegrationFact]
    public async Task Create_BitisTarihiBaslangictanOncekiTarihseReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var candidateId = $"kan46-exp-invaliddate-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var client = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(client, candidateId);

        var response = await PostAsync(
            client,
            candidateId,
            new Dictionary<string, string>
            {
                ["CompanyName"] = "Tutarsız Tarih A.Ş.",
                ["JobTitle"] = "Uzman",
                ["StartDate"] = "2022-01-01",
                ["EndDate"] = "2020-01-01"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = CreateRawContext();
        var experienceCount = await context.CandidateExperiences
            .CountAsync(e => e.CandidateProfileId == profileId);
        Assert.Equal(0, experienceCount);
    }

    [SqlServerIntegrationFact]
    public async Task Edit_BaskaAdayinKaydiniDuzenleyemezNotFoundDoner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var candidateAId = $"kan46-exp-a-{runId}";
        var candidateBId = $"kan46-exp-b-{runId}";
        await CreateCandidateUserAsync(candidateAId);
        await CreateCandidateUserAsync(candidateBId);
        using var client = CreateClient(factory);
        var profileAId = await CreateCandidateProfileAsync(client, candidateAId);
        await CreateCandidateProfileAsync(client, candidateBId);

        await PostAsync(
            client,
            candidateAId,
            new Dictionary<string, string>
            {
                ["CompanyName"] = "Aday A Şirketi",
                ["JobTitle"] = "Geliştirici",
                ["StartDate"] = "2019-01-01",
                ["IsOngoing"] = "true"
            });

        await using var context = CreateRawContext();
        var experienceId = (await context.CandidateExperiences
            .SingleAsync(e => e.CandidateProfileId == profileAId)).Id;

        using var bClient = CreateClient(factory);
        var token = await GetAntiforgeryTokenAsync(bClient, candidateBId, "/CandidateExperiences/Create");

        using var editRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/CandidateExperiences/Edit/{experienceId}");
        editRequest.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        editRequest.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateBId);
        editRequest.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Id"] = experienceId.ToString(),
                ["CompanyName"] = "Ele Geçirilmiş Kayıt",
                ["JobTitle"] = "Ele Geçirilmiş Unvan",
                ["StartDate"] = "2019-01-01",
                ["IsOngoing"] = "true",
                ["__RequestVerificationToken"] = token
            });

        var editResponse = await bClient.SendAsync(editRequest);

        // Controller returns NotFound(); bilinen KAN-92 hatası (ErrorController'ın yalnızca
        // GET kabul etmesi) bu body'siz yanıtı POST'larda 405'e çevirebiliyor. Buradaki asıl
        // güvenlik kontrolü aşağıdaki "kayıt değişmedi" doğrulaması.
        Assert.True(
            editResponse.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Beklenmeyen durum kodu: {editResponse.StatusCode}");

        var untouched = await context.CandidateExperiences.SingleAsync(e => e.Id == experienceId);
        Assert.Equal("Aday A Şirketi", untouched.CompanyName);
    }

    [SqlServerIntegrationFact]
    public async Task Delete_BaskaAdayinKaydiniSilemezNotFoundDoner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var candidateAId = $"kan46-exp-del-a-{runId}";
        var candidateBId = $"kan46-exp-del-b-{runId}";
        await CreateCandidateUserAsync(candidateAId);
        await CreateCandidateUserAsync(candidateBId);
        using var client = CreateClient(factory);
        var profileAId = await CreateCandidateProfileAsync(client, candidateAId);
        await CreateCandidateProfileAsync(client, candidateBId);

        await PostAsync(
            client,
            candidateAId,
            new Dictionary<string, string>
            {
                ["CompanyName"] = "Silinmemesi Gereken Şirket",
                ["JobTitle"] = "Geliştirici",
                ["StartDate"] = "2019-01-01",
                ["IsOngoing"] = "true"
            });

        await using var context = CreateRawContext();
        var experienceId = (await context.CandidateExperiences
            .SingleAsync(e => e.CandidateProfileId == profileAId)).Id;

        using var bClient = CreateClient(factory);
        var token = await GetAntiforgeryTokenAsync(bClient, candidateBId, "/CandidateExperiences/Create");

        using var deleteRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/CandidateExperiences/Delete/{experienceId}");
        deleteRequest.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        deleteRequest.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateBId);
        deleteRequest.Content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token });

        var deleteResponse = await bClient.SendAsync(deleteRequest);

        // Controller returns NotFound(); bilinen KAN-92 hatası (ErrorController'ın yalnızca
        // GET kabul etmesi) bu body'siz yanıtı POST'larda 405'e çevirebiliyor. Buradaki asıl
        // güvenlik kontrolü aşağıdaki "kayıt hâlâ var" doğrulaması.
        Assert.True(
            deleteResponse.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Beklenmeyen durum kodu: {deleteResponse.StatusCode}");

        var stillExists = await context.CandidateExperiences.AnyAsync(e => e.Id == experienceId);
        Assert.True(stillExists);
    }

    [SqlServerIntegrationFact]
    public async Task Edit_SahibiKendiKaydiniGunceller()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var candidateId = $"kan46-exp-owner-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var client = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(client, candidateId);

        await PostAsync(
            client,
            candidateId,
            new Dictionary<string, string>
            {
                ["CompanyName"] = "İlk Şirket Adı",
                ["JobTitle"] = "Geliştirici",
                ["StartDate"] = "2019-01-01",
                ["IsOngoing"] = "true"
            });

        await using var context = CreateRawContext();
        var experienceId = (await context.CandidateExperiences
            .SingleAsync(e => e.CandidateProfileId == profileId)).Id;

        var token = await GetAntiforgeryTokenAsync(
            client,
            candidateId,
            $"/CandidateExperiences/Edit/{experienceId}");

        using var editRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/CandidateExperiences/Edit/{experienceId}");
        editRequest.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        editRequest.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateId);
        editRequest.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Id"] = experienceId.ToString(),
                ["CompanyName"] = "Güncellenmiş Şirket Adı",
                ["JobTitle"] = "Geliştirici",
                ["StartDate"] = "2019-01-01",
                ["IsOngoing"] = "true",
                ["__RequestVerificationToken"] = token
            });

        var editResponse = await client.SendAsync(editRequest);

        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);

        await using var verificationContext = CreateRawContext();
        var updated = await verificationContext.CandidateExperiences.SingleAsync(e => e.Id == experienceId);
        Assert.Equal("Güncellenmiş Şirket Adı", updated.CompanyName);
    }

    private static async Task CreateCandidateUserAsync(string candidateId)
    {
        await using var context = CreateRawContext();
        context.Users.Add(
            new ApplicationUser
            {
                Id = candidateId,
                UserName = candidateId,
                NormalizedUserName = candidateId.ToUpperInvariant(),
                Email = $"{candidateId}@example.test",
                NormalizedEmail = $"{candidateId}@example.test".ToUpperInvariant()
            });
        await context.SaveChangesAsync();
    }

    private static async Task<int> CreateCandidateProfileAsync(HttpClient client, string candidateId)
    {
        var token = await GetAntiforgeryTokenAsync(client, candidateId, "/CandidateProfile");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/CandidateProfile");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateId);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["FirstName"] = "Test",
                ["LastName"] = "Aday",
                ["__RequestVerificationToken"] = token
            });

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var profile = await context.CandidateProfiles
            .SingleAsync(p => p.ApplicationUserId == candidateId);

        return profile.Id;
    }

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string candidateId,
        Dictionary<string, string> formFields)
    {
        var token = await GetAntiforgeryTokenAsync(client, candidateId, "/CandidateExperiences/Create");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/CandidateExperiences/Create");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateId);
        formFields["__RequestVerificationToken"] = token;
        request.Content = new FormUrlEncodedContent(formFields);

        return await client.SendAsync(request);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        string candidateId,
        string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateId);

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
                    "geçici SQL Server aday deneyim entegrasyon testi atlandı.";
            }
        }
    }
}
