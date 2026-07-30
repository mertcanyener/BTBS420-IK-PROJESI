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

public sealed class CandidateEducationsSqlServerIntegrationTests :
    IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN46_TEST_SQLSERVER_CONNECTION_STRING";

    private readonly TestWebApplicationFactory _baseFactory;

    public CandidateEducationsSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task Index_GecerliBilgilerleEgitimOlusurVeAuditKaydeder()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var candidateId = $"kan46-edu-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var client = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(client, candidateId);
        var educationId = await CreateEducationCatalogAsync(factory, $"Kan46-Egitim-{runId}");

        var response = await PostAsync(
            client,
            candidateId,
            new Dictionary<string, string>
            {
                ["EducationId"] = educationId.ToString(),
                ["SchoolName"] = "Örnek Üniversitesi",
                ["FieldOfStudy"] = "Bilgisayar Mühendisliği",
                ["StartDate"] = "2018-09-01",
                ["EndDate"] = "2022-06-30"
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var education = await context.CandidateEducations
            .SingleOrDefaultAsync(e => e.CandidateProfileId == profileId);
        Assert.NotNull(education);
        Assert.Equal("Örnek Üniversitesi", education.SchoolName);
        Assert.Equal(new DateOnly(2022, 6, 30), education.EndDate);

        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityCreated &&
                    l.TargetEntityType == ActivityEntityTypes.CandidateEducation &&
                    l.TargetEntityId == education.Id.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(log);
        Assert.Equal(candidateId, log.CandidateId);
    }

    [SqlServerIntegrationFact]
    public async Task Create_DevamEdenEgitimBitisTarihiOlmadanKaydedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var candidateId = $"kan46-ongoing-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var client = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(client, candidateId);
        var educationId = await CreateEducationCatalogAsync(factory, $"Kan46-DevamEden-{runId}");

        var response = await PostAsync(
            client,
            candidateId,
            new Dictionary<string, string>
            {
                ["EducationId"] = educationId.ToString(),
                ["SchoolName"] = "Devam Eden Üniversite",
                ["StartDate"] = "2023-09-01",
                ["IsOngoing"] = "true"
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var education = await context.CandidateEducations
            .SingleAsync(e => e.CandidateProfileId == profileId);
        Assert.Null(education.EndDate);
    }

    [SqlServerIntegrationFact]
    public async Task Create_BitisTarihiBaslangictanOncekiTarihseReddedilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var candidateId = $"kan46-invaliddate-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var client = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(client, candidateId);
        var educationId = await CreateEducationCatalogAsync(factory, $"Kan46-GecersizTarih-{runId}");

        var response = await PostAsync(
            client,
            candidateId,
            new Dictionary<string, string>
            {
                ["EducationId"] = educationId.ToString(),
                ["SchoolName"] = "Tutarsız Tarih Üniversitesi",
                ["StartDate"] = "2022-09-01",
                ["EndDate"] = "2020-06-30"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = CreateRawContext();
        var educationCount = await context.CandidateEducations
            .CountAsync(e => e.CandidateProfileId == profileId);
        Assert.Equal(0, educationCount);
    }

    [SqlServerIntegrationFact]
    public async Task Edit_BaskaAdayinKaydiniDuzenleyemezNotFoundDoner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var candidateAId = $"kan46-edu-a-{runId}";
        var candidateBId = $"kan46-edu-b-{runId}";
        await CreateCandidateUserAsync(candidateAId);
        await CreateCandidateUserAsync(candidateBId);
        using var client = CreateClient(factory);
        var profileAId = await CreateCandidateProfileAsync(client, candidateAId);
        await CreateCandidateProfileAsync(client, candidateBId);
        var educationId = await CreateEducationCatalogAsync(factory, $"Kan46-Yatay-{runId}");

        await PostAsync(
            client,
            candidateAId,
            new Dictionary<string, string>
            {
                ["EducationId"] = educationId.ToString(),
                ["SchoolName"] = "Aday A Üniversitesi",
                ["StartDate"] = "2019-09-01",
                ["IsOngoing"] = "true"
            });

        await using var context = CreateRawContext();
        var educationRecordId = (await context.CandidateEducations
            .SingleAsync(e => e.CandidateProfileId == profileAId)).Id;

        using var bClient = CreateClient(factory);
        var token = await GetAntiforgeryTokenAsync(bClient, candidateBId, "/CandidateEducations/Create");

        using var editRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/CandidateEducations/Edit/{educationRecordId}");
        editRequest.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        editRequest.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateBId);
        editRequest.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Id"] = educationRecordId.ToString(),
                ["EducationId"] = educationId.ToString(),
                ["SchoolName"] = "Ele Geçirilmiş Kayıt",
                ["StartDate"] = "2019-09-01",
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

        var untouchedEducation = await context.CandidateEducations.SingleAsync(e => e.Id == educationRecordId);
        Assert.Equal("Aday A Üniversitesi", untouchedEducation.SchoolName);
    }

    [SqlServerIntegrationFact]
    public async Task Delete_BaskaAdayinKaydiniSilemezNotFoundDoner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var candidateAId = $"kan46-del-a-{runId}";
        var candidateBId = $"kan46-del-b-{runId}";
        await CreateCandidateUserAsync(candidateAId);
        await CreateCandidateUserAsync(candidateBId);
        using var client = CreateClient(factory);
        var profileAId = await CreateCandidateProfileAsync(client, candidateAId);
        await CreateCandidateProfileAsync(client, candidateBId);
        var educationId = await CreateEducationCatalogAsync(factory, $"Kan46-SilmeYatay-{runId}");

        await PostAsync(
            client,
            candidateAId,
            new Dictionary<string, string>
            {
                ["EducationId"] = educationId.ToString(),
                ["SchoolName"] = "Silinmemesi Gereken Üniversite",
                ["StartDate"] = "2019-09-01",
                ["IsOngoing"] = "true"
            });

        await using var context = CreateRawContext();
        var educationRecordId = (await context.CandidateEducations
            .SingleAsync(e => e.CandidateProfileId == profileAId)).Id;

        using var bClient = CreateClient(factory);
        var token = await GetAntiforgeryTokenAsync(bClient, candidateBId, "/CandidateEducations/Create");

        using var deleteRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/CandidateEducations/Delete/{educationRecordId}");
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

        var stillExists = await context.CandidateEducations.AnyAsync(e => e.Id == educationRecordId);
        Assert.True(stillExists);
    }

    [SqlServerIntegrationFact]
    public async Task Edit_SahibiKendiKaydiniGunceller()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var candidateId = $"kan46-owner-edit-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var client = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(client, candidateId);
        var educationId = await CreateEducationCatalogAsync(factory, $"Kan46-SahipDuzenle-{runId}");

        await PostAsync(
            client,
            candidateId,
            new Dictionary<string, string>
            {
                ["EducationId"] = educationId.ToString(),
                ["SchoolName"] = "İlk Okul Adı",
                ["StartDate"] = "2019-09-01",
                ["IsOngoing"] = "true"
            });

        await using var context = CreateRawContext();
        var educationRecordId = (await context.CandidateEducations
            .SingleAsync(e => e.CandidateProfileId == profileId)).Id;

        var token = await GetAntiforgeryTokenAsync(
            client,
            candidateId,
            $"/CandidateEducations/Edit/{educationRecordId}");

        using var editRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/CandidateEducations/Edit/{educationRecordId}");
        editRequest.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        editRequest.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateId);
        editRequest.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Id"] = educationRecordId.ToString(),
                ["EducationId"] = educationId.ToString(),
                ["SchoolName"] = "Güncellenmiş Okul Adı",
                ["StartDate"] = "2019-09-01",
                ["IsOngoing"] = "true",
                ["__RequestVerificationToken"] = token
            });

        var editResponse = await client.SendAsync(editRequest);

        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);

        await using var verificationContext = CreateRawContext();
        var updated = await verificationContext.CandidateEducations.SingleAsync(e => e.Id == educationRecordId);
        Assert.Equal("Güncellenmiş Okul Adı", updated.SchoolName);
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

    private static async Task<int> CreateEducationCatalogAsync(
        WebApplicationFactory<Program> factory,
        string name)
    {
        using var client = CreateClient(factory);
        var token = await GetAntiforgeryTokenForRoleAsync(client, "/Educations/Create", SystemRoles.Admin);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Educations/Create");
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
        var education = await context.Educations.SingleAsync(e => e.Name == name);

        return education.Id;
    }

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string candidateId,
        Dictionary<string, string> formFields)
    {
        var token = await GetAntiforgeryTokenAsync(client, candidateId, "/CandidateEducations/Create");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/CandidateEducations/Create");
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

    private static async Task<string> GetAntiforgeryTokenForRoleAsync(
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
                    "geçici SQL Server aday eğitim entegrasyon testi atlandı.";
            }
        }
    }
}
