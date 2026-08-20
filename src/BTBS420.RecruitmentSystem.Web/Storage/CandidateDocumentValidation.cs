using System.Collections.Frozen;
using Microsoft.AspNetCore.Http;

namespace BTBS420.RecruitmentSystem.Web.Storage;

public sealed record CandidateDocumentValidationResult(
    bool IsValid,
    string? ErrorMessage,
    string? Extension,
    string? ContentType)
{
    public static CandidateDocumentValidationResult Success(string extension, string contentType)
    {
        return new CandidateDocumentValidationResult(true, null, extension, contentType);
    }

    public static CandidateDocumentValidationResult Failure(string errorMessage)
    {
        return new CandidateDocumentValidationResult(false, errorMessage, null, null);
    }
}

public static class CandidateDocumentValidation
{
    private static readonly FrozenDictionary<string, string> ContentTypesByExtension =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".doc"] = "application/msword",
            [".docx"] =
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png"
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, byte[][]> SignaturesByExtension =
        new Dictionary<string, byte[][]>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = [[0x25, 0x50, 0x44, 0x46]],
            [".doc"] = [[0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]],
            [".docx"] = [[0x50, 0x4B, 0x03, 0x04]],
            [".jpg"] = [[0xFF, 0xD8, 0xFF]],
            [".jpeg"] = [[0xFF, 0xD8, 0xFF]],
            [".png"] = [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]]
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private const int MaxSignatureLength = 8;

    public static async Task<CandidateDocumentValidationResult> ValidateAsync(
        IFormFile file,
        long maxFileSizeBytes,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            return CandidateDocumentValidationResult.Failure("Dosya boş olamaz.");
        }

        if (file.Length > maxFileSizeBytes)
        {
            return CandidateDocumentValidationResult.Failure(
                $"Dosya boyutu en fazla {maxFileSizeBytes / (1024 * 1024)} MB olabilir.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) ||
            !ContentTypesByExtension.TryGetValue(extension, out var expectedContentType))
        {
            return CandidateDocumentValidationResult.Failure(
                "Desteklenmeyen dosya türü. İzin verilenler: PDF, DOC, DOCX, JPG, PNG.");
        }

        if (!string.Equals(file.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
        {
            return CandidateDocumentValidationResult.Failure(
                "Beyan edilen dosya türü (MIME), dosya uzantısıyla uyuşmuyor.");
        }

        var signatures = SignaturesByExtension[extension];
        var header = new byte[MaxSignatureLength];

        await using (var stream = file.OpenReadStream())
        {
            var totalRead = 0;
            while (totalRead < header.Length)
            {
                var read = await stream.ReadAsync(
                    header.AsMemory(totalRead, header.Length - totalRead),
                    cancellationToken);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }
        }

        var matchesSignature = signatures.Any(
            signature => header.Take(signature.Length).SequenceEqual(signature));

        if (!matchesSignature)
        {
            return CandidateDocumentValidationResult.Failure(
                "Dosya içeriği, beyan edilen dosya türüyle uyuşmuyor.");
        }

        return CandidateDocumentValidationResult.Success(extension.ToLowerInvariant(), expectedContentType);
    }
}
