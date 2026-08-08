using System.Text;
using BTBS420.RecruitmentSystem.Web.Ai.Evaluation.CvParsing;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class CvTextExtractionServiceTests
{
    private readonly CvTextExtractionService _service = new();

    [Fact]
    public async Task ExtractAsync_GecerliPdf_MetinVeHashDoner()
    {
        await using var content = File.OpenRead(GetFixturePath("sample-cv.pdf"));

        var result = await _service.ExtractAsync(content, ".pdf");

        Assert.Contains("DENEYIM", result.Text);
        Assert.Contains("EGITIM", result.Text);
        Assert.Matches("^[0-9a-f]{64}$", result.SourceDocumentHash);
    }

    [Fact]
    public async Task ExtractAsync_GecerliDocx_MetinDoner()
    {
        await using var content = CreateSampleDocx("DENEYIM\nAcme AS - Yazilim Muhendisi 01/2020 - 03/2022");

        var result = await _service.ExtractAsync(content, ".docx");

        Assert.Contains("DENEYIM", result.Text);
        Assert.Matches("^[0-9a-f]{64}$", result.SourceDocumentHash);
    }

    [Fact]
    public async Task ExtractAsync_BosIcerik_EmptyNedeniyleHataFirlatir()
    {
        using var content = new MemoryStream();

        var exception = await Assert.ThrowsAsync<CvAnalysisException>(
            () => _service.ExtractAsync(content, ".pdf"));

        Assert.Equal(CvAnalysisFailureReason.Empty, exception.Reason);
    }

    [Fact]
    public async Task ExtractAsync_DesteklenmeyenFormat_UnsupportedFormatNedeniyleHataFirlatir()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("herhangi bir icerik"));

        var exception = await Assert.ThrowsAsync<CvAnalysisException>(
            () => _service.ExtractAsync(content, ".txt"));

        Assert.Equal(CvAnalysisFailureReason.UnsupportedFormat, exception.Reason);
    }

    [Fact]
    public async Task ExtractAsync_BozukPdf_CorruptedNedeniyleHataFirlatirVeHamMesajSizdirmaz()
    {
        using var content = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4\nbu gecerli bir pdf degil"));

        var exception = await Assert.ThrowsAsync<CvAnalysisException>(
            () => _service.ExtractAsync(content, ".pdf"));

        Assert.Equal(CvAnalysisFailureReason.Corrupted, exception.Reason);
        Assert.Equal("CV dosyası okunamadı veya bozuk.", exception.Message);
    }

    private static string GetFixturePath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", "Cv", fileName);
    }

    private static MemoryStream CreateSampleDocx(string text)
    {
        var stream = new MemoryStream();

        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(
                new Body(
                    new Paragraph(
                        new Run(
                            new Text(text)))));
            mainPart.Document.Save();
        }

        stream.Position = 0;
        return stream;
    }
}
