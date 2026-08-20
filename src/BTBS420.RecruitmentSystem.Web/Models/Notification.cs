using System.Collections.Frozen;
using System.Globalization;

namespace BTBS420.RecruitmentSystem.Web.Models;

public sealed class Notification
{
    public const int MaximumRecipientUserIdLength = 450;
    public const int MaximumEventKeyLength = 256;
    public const int MaximumTitleLength = 200;
    public const int MaximumMessageLength = 1000;

    private static readonly FrozenSet<string> SensitiveEventKeySegments =
        new[]
        {
            "authorization",
            "connectionstring",
            "content",
            "cookie",
            "credential",
            "cv",
            "documentcontent",
            "email",
            "filecontent",
            "mail",
            "parola",
            "passwd",
            "password",
            "pwd",
            "resume",
            "secret",
            "token"
        }.ToFrozenSet(StringComparer.Ordinal);

    private Notification()
    {
    }

    internal Notification(
        string recipientUserId,
        string eventKey,
        string title,
        string message,
        DateTimeOffset createdAtUtc)
    {
        RecipientUserId = NormalizeRecipientUserId(recipientUserId);
        EventKey = NormalizeEventKey(eventKey);
        Title = NormalizeRequiredText(title, MaximumTitleLength, nameof(title));
        Message = NormalizeRequiredText(message, MaximumMessageLength, nameof(message));
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public long Id { get; private set; }

    public string RecipientUserId { get; private set; } = string.Empty;

    public ApplicationUser RecipientUser { get; private set; } = null!;

    public string EventKey { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    public bool IsRead => ReadAtUtc.HasValue;

    internal static string NormalizeRecipientUserId(string recipientUserId)
    {
        return NormalizeRequiredText(
            recipientUserId,
            MaximumRecipientUserIdLength,
            nameof(recipientUserId));
    }

    internal static string NormalizeEventKey(string eventKey)
    {
        if (string.IsNullOrWhiteSpace(eventKey))
        {
            throw new ArgumentException(
                "Bildirim olay anahtarı boş olamaz.",
                nameof(eventKey));
        }

        var normalized = eventKey.Trim().ToLowerInvariant();

        if (normalized.Length > MaximumEventKeyLength)
        {
            throw new ArgumentException(
                $"Bildirim olay anahtarı en fazla {MaximumEventKeyLength} karakter olabilir.",
                nameof(eventKey));
        }

        if (!IsAsciiLetterOrDigit(normalized[0]) ||
            !IsAsciiLetterOrDigit(normalized[^1]) ||
            normalized.Any(
                character =>
                    !IsAsciiLetterOrDigit(character) &&
                    character is not ':' and not '.' and not '_' and not '-'))
        {
            throw new ArgumentException(
                "Bildirim olay anahtarı yalnızca küçük ASCII harf, rakam, " +
                "iki nokta, nokta, alt çizgi ve tire içerebilir; " +
                "harf veya rakamla başlayıp bitmelidir.",
                nameof(eventKey));
        }

        var segments = normalized.Split(
            [':', '.', '_', '-'],
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Any(SensitiveEventKeySegments.Contains))
        {
            throw new ArgumentException(
                "Bildirim olay anahtarı hassas veri belirten bir segment içeremez.",
                nameof(eventKey));
        }

        return normalized;
    }

    private static string NormalizeRequiredText(
        string value,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Değer boş olamaz.", parameterName);
        }

        var normalized = value.Trim();

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"Değer en fazla {maximumLength} karakter olabilir.",
                parameterName);
        }

        if (normalized.Any(IsUnsafeTextCharacter))
        {
            throw new ArgumentException(
                "Değer kontrol veya görünmez yönlendirme karakteri içeremez.",
                parameterName);
        }

        return normalized;
    }

    private static bool IsAsciiLetterOrDigit(char character)
    {
        return character is >= 'a' and <= 'z' or >= '0' and <= '9';
    }

    private static bool IsUnsafeTextCharacter(char character)
    {
        return char.IsControl(character) ||
               char.GetUnicodeCategory(character) is
                   UnicodeCategory.Format or
                   UnicodeCategory.LineSeparator or
                   UnicodeCategory.ParagraphSeparator;
    }
}
