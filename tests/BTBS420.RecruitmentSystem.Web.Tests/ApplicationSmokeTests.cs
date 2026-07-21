using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class ApplicationSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApplicationSmokeTests(WebApplicationFactory<Program> factory)
    {
        _client = factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseContentRoot(FindWebContentRoot());
                builder.ConfigureLogging(logging => logging.ClearProviders());
            })
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });
    }

    [Fact]
    public async Task AnaRota_BootstrapKulluguIleBasariliDoner()
    {
        var response = await _client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("İşe Alım ve Aday Takip Sistemi", content);
        Assert.Contains("name=\"viewport\"", content);
        Assert.Contains("bootstrap.min.css", content);
        Assert.Contains("bootstrap.bundle.min.js", content);
        Assert.Contains("navbar-toggler", content);
    }

    [Theory]
    [InlineData("/Error/403", HttpStatusCode.Forbidden, "Erişim reddedildi")]
    [InlineData("/Error/404", HttpStatusCode.NotFound, "Sayfa bulunamadı")]
    [InlineData("/Error/500", HttpStatusCode.InternalServerError, "Beklenmeyen bir hata oluştu")]
    public async Task HataSayfalari_DogruDurumVeMesajiDoner(
        string path,
        HttpStatusCode expectedStatusCode,
        string expectedMessage)
    {
        var response = await _client.GetAsync(path);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.Contains(expectedMessage, content);
    }

    [Fact]
    public async Task BilinmeyenRota_KullaniciDostu404SayfasiniDoner()
    {
        var response = await _client.GetAsync("/bulunmayan-sayfa");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Sayfa bulunamadı", content);
    }

    [Theory]
    [InlineData("/lib/bootstrap/dist/css/bootstrap.min.css")]
    [InlineData("/lib/bootstrap/dist/js/bootstrap.bundle.min.js")]
    public async Task BootstrapDosyalari_Yuklenebilir(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
