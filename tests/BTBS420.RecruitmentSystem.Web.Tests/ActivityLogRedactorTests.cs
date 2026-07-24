using BTBS420.RecruitmentSystem.Web.ActivityLogging;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class ActivityLogRedactorTests
{
    [Theory]
    [InlineData("password=KAN23-Password; işlem başarılı", "KAN23-Password")]
    [InlineData("""{"access_token":"KAN23-AccessToken","status":"ok"}""", "KAN23-AccessToken")]
    [InlineData("Authorization: Bearer KAN23-Authorization", "KAN23-Authorization")]
    [InlineData("Cookie=session=KAN23-Cookie", "KAN23-Cookie")]
    [InlineData(
        "ConnectionString=Server=localhost;Password=KAN23-DatabasePassword;",
        "KAN23-DatabasePassword")]
    [InlineData(
        "ConnectionString=Server=db;Database=Prod;AccountKey=KAN23-ConnectionLeak",
        "KAN23-ConnectionLeak")]
    [InlineData(
        "ConnectionString=Password={abc};AccountKey=KAN23-BracedLeak",
        "KAN23-BracedLeak")]
    [InlineData(
        "Cookie=session=a;customAuth=KAN23-CookieLeak",
        "KAN23-CookieLeak")]
    [InlineData("documentContent=KAN23-DocumentBody", "KAN23-DocumentBody")]
    [InlineData(
        "documentContent=ilk;KAN23-DocumentLeak",
        "KAN23-DocumentLeak")]
    [InlineData(
        """documentContent={"outer":{"x":"a"},"tail":"KAN23-NestedLeak"}""",
        "KAN23-NestedLeak")]
    [InlineData(
        "fileContent=ilk,KAN23-FileLeak",
        "KAN23-FileLeak")]
    [InlineData("""{"passwordHash":"KAN23-PasswordHash","status":"ok"}""", "KAN23-PasswordHash")]
    [InlineData("""{"privateKey":"KAN23-PrivateKey"}""", "KAN23-PrivateKey")]
    [InlineData("""{"tokenHash":"KAN23-TokenHash"}""", "KAN23-TokenHash")]
    [InlineData(
        """{"password":"abc\"KAN23-EscapedTail","status":"ok"}""",
        "KAN23-EscapedTail")]
    [InlineData("Bearer KAN23-BearerValue", "KAN23-BearerValue")]
    [InlineData(
        "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJLQU4tMjMifQ.KAN23SignatureValue",
        "KAN23SignatureValue")]
    public void Redact_HassasDegeriKayitMetnindenCikarir(
        string summary,
        string sensitiveValue)
    {
        var redactor = new ActivityLogRedactor();

        var result = redactor.Redact(summary);

        Assert.Contains(ActivityLogRedactor.RedactedValue, result);
        Assert.False(result.Contains(sensitiveValue, StringComparison.Ordinal));
    }

    [Fact]
    public void Redact_GuvenliMetniKorurVeKontrolKarakterleriniTemizler()
    {
        var redactor = new ActivityLogRedactor();

        var result = redactor.Redact(
            "Aday durumu güncellendi.\r\nSahte kayıt satırı\0");

        Assert.Equal(
            "Aday durumu güncellendi. Sahte kayıt satırı",
            result);
        Assert.DoesNotContain('\r', result);
        Assert.DoesNotContain('\n', result);
        Assert.DoesNotContain('\0', result);
    }

    [Fact]
    public void Redact_OzetiGuvenliUzunluktaSinirlar()
    {
        var redactor = new ActivityLogRedactor();

        var result = redactor.Redact(
            new string('A', ActivityLogRedactor.MaximumSummaryLength + 50));

        Assert.Equal(ActivityLogRedactor.MaximumSummaryLength, result.Length);
    }

    [Fact]
    public void Redact_UnicodeAyiriciVeYonlendirmeKarakterleriniTemizler()
    {
        var redactor = new ActivityLogRedactor();

        var result = redactor.Redact(
            "Başlangıç\u2028sahte\u2029satır\u202Eters\u2066izole");

        Assert.Equal("Başlangıç sahte satır ters izole", result);
        Assert.DoesNotContain('\u2028', result);
        Assert.DoesNotContain('\u2029', result);
        Assert.DoesNotContain('\u202E', result);
        Assert.DoesNotContain('\u2066', result);
    }

    [Fact]
    public void Redact_IslemeSinirindaKesilenJwtOnekiniKayittanCikarir()
    {
        var redactor = new ActivityLogRedactor();
        var jwtPrefix = "eyJ" + new string('A', 5000);

        var result = redactor.Redact($"İşlem özeti {jwtPrefix}");

        Assert.Contains(ActivityLogRedactor.RedactedValue, result);
        Assert.DoesNotContain("eyJ", result, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('A', 100), result, StringComparison.Ordinal);
    }
}
