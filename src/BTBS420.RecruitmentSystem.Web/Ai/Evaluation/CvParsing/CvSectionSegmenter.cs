namespace BTBS420.RecruitmentSystem.Web.Ai.Evaluation.CvParsing;

public static class CvSectionSegmenter
{
    private const int MaximumHeaderLineLength = 50;

    private static readonly IReadOnlyDictionary<CvSectionKind, string[]> HeaderKeywords =
        new Dictionary<CvSectionKind, string[]>
        {
            [CvSectionKind.Summary] =
                ["OZET", "PROFIL", "PROFILE", "HAKKIMDA", "ABOUT", "SUMMARY", "OBJECTIVE"],
            [CvSectionKind.Experience] =
                ["DENEYIM", "IS DENEYIMI", "EXPERIENCE", "WORK EXPERIENCE"],
            [CvSectionKind.Education] =
                ["EGITIM", "EDUCATION"],
            [CvSectionKind.Skills] =
                ["BECERILER", "YETENEKLER", "SKILLS"],
            [CvSectionKind.Languages] =
                ["DIL", "DILLER", "LANGUAGES"],
            [CvSectionKind.Certifications] =
                ["SERTIFIKA", "SERTIFIKALAR", "CERTIFICATIONS", "CERTIFICATES"],
            [CvSectionKind.Projects] =
                ["PROJE", "PROJELER", "PROJECTS"],
            [CvSectionKind.Achievements] =
                ["BASARI", "BASARILAR", "ODULLER", "ACHIEVEMENTS", "AWARDS"],
        };

    public static IReadOnlyList<CvSection> Segment(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var sections = new List<CvSection>();
        CvSectionKind? currentKind = null;
        var currentEntries = new List<string>();

        void FlushCurrent()
        {
            if (currentKind is { } kind && currentEntries.Count > 0)
            {
                sections.Add(new CvSection(kind, currentEntries));
            }

            currentEntries = [];
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var matchedKind = TryMatchHeader(line);
            if (matchedKind is not null)
            {
                FlushCurrent();
                currentKind = matchedKind;
                continue;
            }

            currentEntries.Add(line);
        }

        FlushCurrent();

        return sections;
    }

    private static CvSectionKind? TryMatchHeader(string line)
    {
        if (line.Length > MaximumHeaderLineLength)
        {
            return null;
        }

        var normalized = Normalize(line.TrimEnd(':').Trim());

        foreach (var (kind, keywords) in HeaderKeywords)
        {
            if (Array.IndexOf(keywords, normalized) >= 0)
            {
                return kind;
            }
        }

        return null;
    }

    private static string Normalize(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);

        foreach (var character in value)
        {
            builder.Append(character switch
            {
                'İ' or 'ı' or 'i' or 'I' => 'I',
                'Ş' or 'ş' => 'S',
                'Ğ' or 'ğ' => 'G',
                'Ü' or 'ü' => 'U',
                'Ö' or 'ö' => 'O',
                'Ç' or 'ç' => 'C',
                _ => char.ToUpperInvariant(character),
            });
        }

        return builder.ToString();
    }
}
