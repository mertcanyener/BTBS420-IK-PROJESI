using System.Collections.Frozen;

namespace BTBS420.RecruitmentSystem.Web.Models;

public static class CandidateDocumentTypes
{
    public const string Resume = "resume";
    public const string CoverLetter = "cover-letter";
    public const string Certificate = "certificate";
    public const string Other = "other";

    private static readonly FrozenSet<string> DefinedTypes =
        new[] { Resume, CoverLetter, Certificate, Other }
            .ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => DefinedTypes;

    public static bool IsDefined(string documentType)
    {
        return DefinedTypes.Contains(documentType);
    }

    public static string GetDisplayLabel(string documentType)
    {
        return documentType switch
        {
            Resume => "Özgeçmiş (CV)",
            CoverLetter => "Ön Yazı",
            Certificate => "Sertifika",
            Other => "Diğer",
            _ => documentType
        };
    }
}
