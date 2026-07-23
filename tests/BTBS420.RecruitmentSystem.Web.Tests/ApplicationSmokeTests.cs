using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class ApplicationSmokeTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ApplicationSmokeTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task Uygulama_TestOrtamindaAyagaKalkar()
    {
        var environment = _factory.Services.GetRequiredService<IWebHostEnvironment>();
        var response = await _client.GetAsync("/");

        Assert.Equal(TestWebApplicationFactory.EnvironmentName, environment.EnvironmentName);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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

}
