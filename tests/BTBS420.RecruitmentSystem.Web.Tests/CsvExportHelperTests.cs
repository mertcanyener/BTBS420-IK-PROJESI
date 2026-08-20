using System.Text;
using BTBS420.RecruitmentSystem.Web.ActivityLogging;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class CsvExportHelperTests
{
    [Theory]
    [InlineData("=SUM(A1:A2)", "'=SUM(A1:A2)")]
    [InlineData("+1234", "'+1234")]
    [InlineData("-1234", "'-1234")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("\tformula", "'\tformula")]
    public void BuildCsv_FormulTetikleyiciKarakterleBaslayanDegerleriKacar(string rawValue, string expectedContent)
    {
        var csv = ReadCsv([rawValue]);

        var dataLine = GetDataLine(csv);
        Assert.Equal(expectedContent, dataLine);
    }

    [Fact]
    public void BuildCsv_NormalDegerDegistirilmedenYazilir()
    {
        var csv = ReadCsv(["Normal Değer"]);

        Assert.Equal("Normal Değer", GetDataLine(csv));
    }

    [Fact]
    public void BuildCsv_VirgulIcerenDegerTirnakIcineAlinir()
    {
        var csv = ReadCsv(["Ankara, Türkiye"]);

        Assert.Equal("\"Ankara, Türkiye\"", GetDataLine(csv));
    }

    [Fact]
    public void BuildCsv_TirnakIcerenDegerIkiKatTirnaklaKacilir()
    {
        var csv = ReadCsv(["\"alıntı\""]);

        Assert.Equal("\"\"\"alıntı\"\"\"", GetDataLine(csv));
    }

    [Fact]
    public void BuildCsv_FormulKarakteriVeVirgulBirlikteDogruKacilir()
    {
        var csv = ReadCsv(["=A1,B1"]);

        Assert.Equal("\"'=A1,B1\"", GetDataLine(csv));
    }

    [Fact]
    public void BuildCsv_NullDegerBosStringOlarakYazilir()
    {
        var bytes = CsvExportHelper.BuildCsv(["Sütun1", "Sütun2"], [[null, "değer"]]);
        var content = DecodeWithoutBom(bytes);
        var lines = content.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(",değer", lines[1]);
    }

    private static string ReadCsv(string[] values)
    {
        var bytes = CsvExportHelper.BuildCsv(["Sütun1"], [values]);
        return DecodeWithoutBom(bytes);
    }

    private static string DecodeWithoutBom(byte[] bytes)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var hasBom = bytes.Length >= preamble.Length &&
            bytes.Take(preamble.Length).SequenceEqual(preamble);

        return hasBom
            ? Encoding.UTF8.GetString(bytes, preamble.Length, bytes.Length - preamble.Length)
            : Encoding.UTF8.GetString(bytes);
    }

    private static string GetDataLine(string csvContent)
    {
        var lines = csvContent.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        return lines[1];
    }
}
