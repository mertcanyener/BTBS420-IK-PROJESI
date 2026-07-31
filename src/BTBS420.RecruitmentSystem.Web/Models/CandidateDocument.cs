namespace BTBS420.RecruitmentSystem.Web.Models;

public sealed class CandidateDocument
{
    public const int MaximumOriginalFileNameLength = 260;
    public const int MaximumStoredFileNameLength = 64;
    public const int MaximumContentTypeLength = 100;

    private CandidateDocument()
    {
    }

    internal CandidateDocument(
        int candidateProfileId,
        string documentType,
        string originalFileName,
        string storedFileName,
        string contentType,
        long fileSizeBytes,
        DateTime uploadedAtUtc)
    {
        CandidateProfileId = candidateProfileId;
        DocumentType = NormalizeDocumentType(documentType);
        OriginalFileName = NormalizeOriginalFileName(originalFileName);
        StoredFileName = NormalizeStoredFileName(storedFileName);
        ContentType = NormalizeContentType(contentType);
        FileSizeBytes = ValidateFileSizeBytes(fileSizeBytes);
        UploadedAtUtc = uploadedAtUtc;
    }

    public int Id { get; private set; }

    public int CandidateProfileId { get; private set; }

    public CandidateProfile CandidateProfile { get; private set; } = null!;

    public string DocumentType { get; private set; } = string.Empty;

    public string OriginalFileName { get; private set; } = string.Empty;

    public string StoredFileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long FileSizeBytes { get; private set; }

    public DateTime UploadedAtUtc { get; private set; }

    private static string NormalizeDocumentType(string documentType)
    {
        if (!CandidateDocumentTypes.IsDefined(documentType))
        {
            throw new ArgumentException("Geçersiz belge türü.", nameof(documentType));
        }

        return documentType;
    }

    private static string NormalizeOriginalFileName(string originalFileName)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new ArgumentException("Dosya adı boş olamaz.", nameof(originalFileName));
        }

        var normalized = originalFileName.Trim();

        if (normalized.Length > MaximumOriginalFileNameLength)
        {
            throw new ArgumentException(
                $"Dosya adı en fazla {MaximumOriginalFileNameLength} karakter olabilir.",
                nameof(originalFileName));
        }

        return normalized;
    }

    private static string NormalizeStoredFileName(string storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName))
        {
            throw new ArgumentException("Saklama dosya adı boş olamaz.", nameof(storedFileName));
        }

        if (storedFileName.Length > MaximumStoredFileNameLength ||
            storedFileName.IndexOfAny(['/', '\\', ':']) >= 0)
        {
            throw new ArgumentException("Saklama dosya adı geçersiz.", nameof(storedFileName));
        }

        return storedFileName;
    }

    private static string NormalizeContentType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("İçerik türü boş olamaz.", nameof(contentType));
        }

        var normalized = contentType.Trim();

        if (normalized.Length > MaximumContentTypeLength)
        {
            throw new ArgumentException(
                $"İçerik türü en fazla {MaximumContentTypeLength} karakter olabilir.",
                nameof(contentType));
        }

        return normalized;
    }

    private static long ValidateFileSizeBytes(long fileSizeBytes)
    {
        if (fileSizeBytes <= 0)
        {
            throw new ArgumentException("Dosya boyutu sıfırdan büyük olmalıdır.", nameof(fileSizeBytes));
        }

        return fileSizeBytes;
    }
}
