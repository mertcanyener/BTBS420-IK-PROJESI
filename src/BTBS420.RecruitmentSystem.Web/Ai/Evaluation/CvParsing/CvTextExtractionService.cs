using System.Security.Cryptography;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace BTBS420.RecruitmentSystem.Web.Ai.Evaluation.CvParsing;

public sealed class CvTextExtractionService : ICvTextExtractionService
{
    public async Task<CvTextExtractionResult> ExtractAsync(
        Stream content,
        string fileExtension,
        CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        if (bytes.Length == 0)
        {
            throw new CvAnalysisException(CvAnalysisFailureReason.Empty, "CV içeriği boş.");
        }

        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var text = fileExtension.ToLowerInvariant() switch
        {
            ".pdf" => ExtractFromPdf(bytes),
            ".docx" => ExtractFromDocx(bytes),
            _ => throw new CvAnalysisException(
                CvAnalysisFailureReason.UnsupportedFormat,
                $"Desteklenmeyen CV formatı: {fileExtension}"),
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new CvAnalysisException(
                CvAnalysisFailureReason.Empty,
                "CV içeriğinden metin çıkarılamadı.");
        }

        return new CvTextExtractionResult(text, hash);
    }

    private static string ExtractFromPdf(byte[] bytes)
    {
        try
        {
            using var document = PdfDocument.Open(bytes);
            var builder = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                builder.AppendLine(ContentOrderTextExtractor.GetText(page, false));
            }

            return builder.ToString();
        }
        catch (PdfDocumentEncryptedException)
        {
            throw new CvAnalysisException(CvAnalysisFailureReason.Encrypted, "CV dosyası şifreli.");
        }
        catch (Exception exception) when (exception is not CvAnalysisException)
        {
            throw new CvAnalysisException(
                CvAnalysisFailureReason.Corrupted,
                "CV dosyası okunamadı veya bozuk.");
        }
    }

    private static string ExtractFromDocx(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            using var document = WordprocessingDocument.Open(stream, false);

            return document.MainDocumentPart?.Document?.Body?.InnerText ?? string.Empty;
        }
        catch (Exception exception) when (exception is not CvAnalysisException)
        {
            throw new CvAnalysisException(
                CvAnalysisFailureReason.Corrupted,
                "CV dosyası okunamadı veya bozuk.");
        }
    }
}
