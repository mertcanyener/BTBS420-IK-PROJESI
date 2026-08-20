using System.Text.RegularExpressions;

namespace BTBS420.RecruitmentSystem.Web.ActivityLogging;

public sealed partial class ActivityLogRedactor : IActivityLogRedactor
{
    public const int MaximumSummaryLength = 1000;
    public const string RedactedValue = "[REDACTED]";
    private const int MaximumInputLength = MaximumSummaryLength * 4;

    public string Redact(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return string.Empty;
        }

        var boundedSummary = summary.Length <= MaximumInputLength
            ? summary
            : summary[..MaximumInputLength];
        var redacted = ControlCharactersPattern().Replace(boundedSummary, " ");
        redacted = SensitiveKeyValuePattern().Replace(
            redacted,
            match => $"{match.Groups["prefix"].Value}{RedactedValue}");
        redacted = AuthorizationValuePattern().Replace(
            redacted,
            match => $"{match.Groups["scheme"].Value} {RedactedValue}");
        redacted = JsonWebTokenPattern().Replace(redacted, RedactedValue);
        redacted = RepeatedWhitespacePattern().Replace(redacted, " ").Trim();

        return redacted.Length <= MaximumSummaryLength
            ? redacted
            : redacted[..MaximumSummaryLength];
    }

    [GeneratedRegex(
        @"[\p{Cc}\p{Cf}\p{Zl}\p{Zp}]+",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex ControlCharactersPattern();

    [GeneratedRegex(
        @"(?<prefix>[""']?(?:password(?:[_-]?(?:hash|salt))?|parola(?:[_-]?hash)?|şifre(?:[_-]?hash)?|passphrase|pwd|token(?:[_-]?hash)?|access[_-]?token|refresh[_-]?token|secret|client[_-]?secret|api[_-]?key|authorization|cookie|set-cookie|connection(?:string|[_-]?string)|private[_-]?key|security[_-]?stamp|document(?:content|[_-]?content)?|file(?:content|[_-]?content)?|content|cv|resume|belge(?:\s+|[_-]?)içeriği|özgeçmiş)[""']?\s*[:=]\s*)(?<value>""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'|[^\r\n]+)",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant |
        RegexOptions.NonBacktracking)]
    private static partial Regex SensitiveKeyValuePattern();

    [GeneratedRegex(
        @"\b(?<scheme>Bearer|Basic)\s+\S+",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant |
        RegexOptions.NonBacktracking)]
    private static partial Regex AuthorizationValuePattern();

    [GeneratedRegex(
        @"\beyJ[A-Za-z0-9_-]{8,}(?:\.[A-Za-z0-9_-]+){0,2}\b",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex JsonWebTokenPattern();

    [GeneratedRegex(
        @"\s{2,}",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex RepeatedWhitespacePattern();
}
