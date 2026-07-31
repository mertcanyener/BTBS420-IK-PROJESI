using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class CandidateDocumentsSqlServerIntegrationTests :
    IClassFixture<TestWebApplicationFactory>
{
    private const string ConnectionStringEnvironmentVariable =
        "KAN46_TEST_SQLSERVER_CONNECTION_STRING";

    private static readonly byte[] ValidPdfBytes =
        System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF");

    private static readonly byte[] InvalidSignatureBytes =
        System.Text.Encoding.ASCII.GetBytes("Bu gerçek bir PDF dosyası değildir.");

    private readonly TestWebApplicationFactory _baseFactory;

    public CandidateDocumentsSqlServerIntegrationTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
    }

    [SqlServerIntegrationFact]
    public async Task Create_GecerliPdfIleBelgeYuklenirVeAuditKaydeder()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var candidateId = $"kan47-doc-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var client = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(client, candidateId);

        var response = await UploadAsync(
            client,
            candidateId,
            CandidateDocumentTypes.Resume,
            "ozgecmis.pdf",
            "application/pdf",
            ValidPdfBytes);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var context = CreateRawContext();
        var document = await context.CandidateDocuments
            .SingleOrDefaultAsync(d => d.CandidateProfileId == profileId);
        Assert.NotNull(document);
        Assert.Equal("ozgecmis.pdf", document.OriginalFileName);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal(ValidPdfBytes.Length, document.FileSizeBytes);

        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityCreated &&
                    l.TargetEntityType == ActivityEntityTypes.CandidateDocument &&
                    l.TargetEntityId == document.Id.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(log);
        Assert.Equal(candidateId, log.CandidateId);
    }

    [SqlServerIntegrationFact]
    public async Task Create_ImzaUyusmayanDosyaReddedilirVeKayitOlusmaz()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var candidateId = $"kan47-badsig-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var client = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(client, candidateId);

        var response = await UploadAsync(
            client,
            candidateId,
            CandidateDocumentTypes.Resume,
            "sahte.pdf",
            "application/pdf",
            InvalidSignatureBytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = CreateRawContext();
        var count = await context.CandidateDocuments.CountAsync(d => d.CandidateProfileId == profileId);
        Assert.Equal(0, count);
    }

    [SqlServerIntegrationFact]
    public async Task Create_BoyutLimitiAsilirsaReddedilir()
    {
        using var factory = CreateSqlFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["CandidateDocumentStorage:MaxFileSizeBytes"] = "10"
                    });
            });
        });

        var runId = Guid.NewGuid().ToString("N");
        var candidateId = $"kan47-toolarge-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var client = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(client, candidateId);

        var response = await UploadAsync(
            client,
            candidateId,
            CandidateDocumentTypes.Resume,
            "buyukdosya.pdf",
            "application/pdf",
            ValidPdfBytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = CreateRawContext();
        var count = await context.CandidateDocuments.CountAsync(d => d.CandidateProfileId == profileId);
        Assert.Equal(0, count);
    }

    [SqlServerIntegrationFact]
    public async Task Delete_BaskaAdayinBelgesiniSilemezNotFoundDoner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var candidateAId = $"kan47-del-a-{runId}";
        var candidateBId = $"kan47-del-b-{runId}";
        await CreateCandidateUserAsync(candidateAId);
        await CreateCandidateUserAsync(candidateBId);
        using var client = CreateClient(factory);
        var profileAId = await CreateCandidateProfileAsync(client, candidateAId);
        await CreateCandidateProfileAsync(client, candidateBId);

        await UploadAsync(
            client,
            candidateAId,
            CandidateDocumentTypes.Resume,
            "silinmemeli.pdf",
            "application/pdf",
            ValidPdfBytes);

        await using var context = CreateRawContext();
        var documentId = (await context.CandidateDocuments
            .SingleAsync(d => d.CandidateProfileId == profileAId)).Id;

        using var bClient = CreateClient(factory);
        var token = await GetAntiforgeryTokenAsync(bClient, candidateBId, "/CandidateDocuments/Create");

        using var deleteRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/CandidateDocuments/Delete/{documentId}");
        deleteRequest.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        deleteRequest.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateBId);
        deleteRequest.Content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token });

        var deleteResponse = await bClient.SendAsync(deleteRequest);

        // Controller NotFound() döner; bilinen KAN-92 hatası (ErrorController'ın yalnızca
        // GET kabul etmesi) bu body'siz yanıtı POST'larda 405'e çevirebiliyor. Asıl güvenlik
        // kontrolü aşağıdaki "kayıt hâlâ var" doğrulaması.
        Assert.True(
            deleteResponse.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Beklenmeyen durum kodu: {deleteResponse.StatusCode}");

        var stillExists = await context.CandidateDocuments.AnyAsync(d => d.Id == documentId);
        Assert.True(stillExists);
    }

    [SqlServerIntegrationFact]
    public async Task Download_SahibiKendiBelgesiniIndirebilirVeAuditKaydeder()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var candidateId = $"kan47-owner-dl-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var client = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(client, candidateId);

        await UploadAsync(
            client,
            candidateId,
            CandidateDocumentTypes.Resume,
            "indirilecek.pdf",
            "application/pdf",
            ValidPdfBytes);

        await using var context = CreateRawContext();
        var documentId = (await context.CandidateDocuments
            .SingleAsync(d => d.CandidateProfileId == profileId)).Id;

        using var downloadRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/CandidateDocuments/Download/{documentId}");
        downloadRequest.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        downloadRequest.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateId);

        var downloadResponse = await client.SendAsync(downloadRequest);

        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(ValidPdfBytes, downloadedBytes);

        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityDownloaded &&
                    l.TargetEntityType == ActivityEntityTypes.CandidateDocument &&
                    l.TargetEntityId == documentId.ToString())
            .FirstOrDefaultAsync();
        Assert.NotNull(log);
        Assert.Equal(candidateId, log.CandidateId);
    }

    [SqlServerIntegrationFact]
    public async Task Download_BaskaAdayinBelgesiniIndiremezNotFoundDoner()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var candidateAId = $"kan47-dl-a-{runId}";
        var candidateBId = $"kan47-dl-b-{runId}";
        await CreateCandidateUserAsync(candidateAId);
        await CreateCandidateUserAsync(candidateBId);
        using var client = CreateClient(factory);
        var profileAId = await CreateCandidateProfileAsync(client, candidateAId);
        await CreateCandidateProfileAsync(client, candidateBId);

        await UploadAsync(
            client,
            candidateAId,
            CandidateDocumentTypes.Resume,
            "gizli.pdf",
            "application/pdf",
            ValidPdfBytes);

        await using var context = CreateRawContext();
        var documentId = (await context.CandidateDocuments
            .SingleAsync(d => d.CandidateProfileId == profileAId)).Id;

        using var bClient = CreateClient(factory);
        using var downloadRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/CandidateDocuments/Download/{documentId}");
        downloadRequest.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        downloadRequest.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateBId);

        var downloadResponse = await bClient.SendAsync(downloadRequest);

        Assert.True(
            downloadResponse.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Beklenmeyen durum kodu: {downloadResponse.StatusCode}");
    }

    [SqlServerIntegrationFact]
    public async Task StaffDownload_YetkiliPersonelHerhangiBirAdayinBelgesiniIndirebilir()
    {
        using var factory = CreateSqlFactory();
        var runId = Guid.NewGuid().ToString("N");
        var candidateId = $"kan47-staffdl-{runId}";
        await CreateCandidateUserAsync(candidateId);
        using var client = CreateClient(factory);
        var profileId = await CreateCandidateProfileAsync(client, candidateId);

        await UploadAsync(
            client,
            candidateId,
            CandidateDocumentTypes.Certificate,
            "sertifika.pdf",
            "application/pdf",
            ValidPdfBytes);

        await using var context = CreateRawContext();
        var documentId = (await context.CandidateDocuments
            .SingleAsync(d => d.CandidateProfileId == profileId)).Id;

        using var staffClient = CreateClient(factory);
        using var downloadRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/StaffCandidateDocuments/Download/{documentId}");
        downloadRequest.Headers.Add(
            TestAuthenticationHandler.RoleHeaderName,
            SystemRoles.RecruitmentSpecialist);

        var downloadResponse = await staffClient.SendAsync(downloadRequest);

        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(ValidPdfBytes, downloadedBytes);

        var log = await context.ActivityLogs
            .Where(
                l =>
                    l.ActionCode == ActivityActionCodes.EntityDownloaded &&
                    l.TargetEntityType == ActivityEntityTypes.CandidateDocument &&
                    l.TargetEntityId == documentId.ToString())
            .ToListAsync();
        Assert.Contains(log, l => l.CandidateId == candidateId);
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

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        string candidateId,
        string documentType,
        string fileName,
        string contentType,
        byte[] fileBytes)
    {
        var token = await GetAntiforgeryTokenAsync(client, candidateId, "/CandidateDocuments/Create");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/CandidateDocuments/Create");
        request.Headers.Add(TestAuthenticationHandler.RoleHeaderName, SystemRoles.Candidate);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, candidateId);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(documentType), "DocumentType");
        content.Add(new StringContent(token), "__RequestVerificationToken");

        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "File", fileName);

        request.Content = content;

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
                    "geçici SQL Server aday belge entegrasyon testi atlandı.";
            }
        }
    }
}
