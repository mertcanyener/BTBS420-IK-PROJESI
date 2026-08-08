using System.Text.RegularExpressions;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.Storage;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Ai.Evaluation.CvParsing;

public sealed class CvAnalysisService(
    ApplicationDbContext dbContext,
    ICandidateDocumentStorageService documentStorageService,
    ICvTextExtractionService textExtractionService) : ICvAnalysisService
{
    private static readonly char[] SegmentSeparators = ['-', '–', '—', '|', ','];

    private static readonly Regex DateRangePattern = new(
        @"(?<startMonth>\d{1,2})?[./]?(?<startYear>\d{4})\s*[-–—]\s*(?:(?<endMonth>\d{1,2})?[./]?(?<endYear>\d{4})|(?<current>Present|Günümüz|Halen|Devam))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<CvAnalysisResult> AnalyzeAsync(
        int candidateProfileId,
        CancellationToken cancellationToken = default)
    {
        var document = await dbContext.CandidateDocuments
            .Where(
                d =>
                    d.CandidateProfileId == candidateProfileId &&
                    d.DocumentType == CandidateDocumentTypes.Resume)
            .OrderByDescending(d => d.UploadedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (document is null)
        {
            throw new CvAnalysisException(
                CvAnalysisFailureReason.NotFound,
                "Aday için analiz edilecek bir CV bulunamadı.");
        }

        var extension = Path.GetExtension(document.OriginalFileName);

        CvTextExtractionResult extraction;
        using (var content = documentStorageService.OpenRead(candidateProfileId, document.StoredFileName))
        {
            extraction = await textExtractionService.ExtractAsync(content, extension, cancellationToken);
        }

        var sections = CvSectionSegmenter.Segment(extraction.Text);
        var entriesByKind = sections
            .GroupBy(section => section.Kind)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.SelectMany(section => section.Entries).ToList());

        var experiences = entriesByKind.TryGetValue(CvSectionKind.Experience, out var experienceEntries)
            ? experienceEntries.Select(TryParseExperienceEntry).OfType<CandidateExperienceSnapshot>().ToList()
            : [];

        var educations = entriesByKind.TryGetValue(CvSectionKind.Education, out var educationEntries)
            ? educationEntries.Select(TryParseEducationEntry).OfType<CandidateEducationSnapshot>().ToList()
            : [];

        var skills = ExtractListItems(entriesByKind, CvSectionKind.Skills);
        var languages = ExtractListItems(entriesByKind, CvSectionKind.Languages);
        var certifications = entriesByKind.GetValueOrDefault(CvSectionKind.Certifications, []);
        var projects = entriesByKind.GetValueOrDefault(CvSectionKind.Projects, []);
        var achievements = entriesByKind.GetValueOrDefault(CvSectionKind.Achievements, []);

        var professionalSummary = entriesByKind.TryGetValue(CvSectionKind.Summary, out var summaryEntries)
            ? string.Join(" ", summaryEntries)
            : string.Empty;

        return new CvAnalysisResult(
            candidateProfileId,
            professionalSummary,
            experiences,
            educations,
            skills,
            languages,
            certifications,
            projects,
            achievements,
            extraction.SourceDocumentHash);
    }

    private static CandidateExperienceSnapshot? TryParseExperienceEntry(string entry)
    {
        var dateMatch = DateRangePattern.Match(entry);
        if (!dateMatch.Success || !TryParseDate(dateMatch, out var startDate, out var endDate))
        {
            return null;
        }

        var segments = SplitIntoSegments(entry.Remove(dateMatch.Index, dateMatch.Length));
        if (segments.Count < 2)
        {
            return null;
        }

        return new CandidateExperienceSnapshot(segments[0], segments[1], startDate, endDate);
    }

    private static CandidateEducationSnapshot? TryParseEducationEntry(string entry)
    {
        var dateMatch = DateRangePattern.Match(entry);
        if (!dateMatch.Success || !TryParseDate(dateMatch, out var startDate, out var endDate))
        {
            return null;
        }

        var segments = SplitIntoSegments(entry.Remove(dateMatch.Index, dateMatch.Length));
        if (segments.Count == 0)
        {
            return null;
        }

        var schoolName = segments[0];
        var educationName = segments.Count > 1 ? segments[1] : segments[0];
        var fieldOfStudy = segments.Count > 2 ? string.Join(" ", segments.Skip(2)) : null;

        return new CandidateEducationSnapshot(schoolName, fieldOfStudy, educationName, startDate, endDate);
    }

    private static bool TryParseDate(Match match, out DateOnly startDate, out DateOnly? endDate)
    {
        startDate = default;
        endDate = null;

        if (!int.TryParse(match.Groups["startYear"].Value, out var startYear))
        {
            return false;
        }

        var startMonth = ParseMonthOrDefault(match.Groups["startMonth"], 1);
        startDate = new DateOnly(startYear, startMonth, 1);

        if (match.Groups["current"].Success)
        {
            return true;
        }

        if (!int.TryParse(match.Groups["endYear"].Value, out var endYear))
        {
            return false;
        }

        var endMonth = ParseMonthOrDefault(match.Groups["endMonth"], 12);
        endDate = new DateOnly(endYear, endMonth, 1);

        return true;
    }

    private static int ParseMonthOrDefault(Group monthGroup, int fallback)
    {
        if (!monthGroup.Success || !int.TryParse(monthGroup.Value, out var month) || month is < 1 or > 12)
        {
            return fallback;
        }

        return month;
    }

    private static IReadOnlyList<string> SplitIntoSegments(string text)
    {
        return text
            .Split(
                SegmentSeparators,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(segment => segment.Length > 0)
            .ToList();
    }

    private static IReadOnlyList<string> ExtractListItems(
        IReadOnlyDictionary<CvSectionKind, IReadOnlyList<string>> entriesByKind,
        CvSectionKind kind)
    {
        if (!entriesByKind.TryGetValue(kind, out var entries))
        {
            return [];
        }

        return entries
            .SelectMany(
                entry => entry.Split(
                    [',', ';'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
