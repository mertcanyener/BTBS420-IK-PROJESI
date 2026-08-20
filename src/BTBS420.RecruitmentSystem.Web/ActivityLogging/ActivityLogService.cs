using System.Globalization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;

namespace BTBS420.RecruitmentSystem.Web.ActivityLogging;

public sealed class ActivityLogService(
    ApplicationDbContext dbContext,
    ICurrentActorAccessor currentActorAccessor,
    IActivityLogRedactor redactor,
    TimeProvider timeProvider) : IActivityLogService
{
    public ActivityLog Stage(ActivityLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!ActivityActionCodes.IsDefined(entry.ActionCode))
        {
            throw new ArgumentException(
                "Aktivite işlem kodu merkezi listede tanımlı olmalıdır.",
                nameof(entry));
        }

        var targetEntityType = NormalizeTargetEntityType(entry.TargetEntityType);
        var targetEntityId = NormalizeIdentifier(
            entry.TargetEntityId,
            128,
            nameof(entry.TargetEntityId));

        if (targetEntityId is not null && targetEntityType is null)
        {
            throw new ArgumentException(
                "Hedef varlık kimliği verildiğinde hedef varlık türü de verilmelidir.",
                nameof(entry));
        }

        var activityLog = new ActivityLog(
            timeProvider.GetUtcNow().ToUniversalTime(),
            NormalizeIdentifier(
                currentActorAccessor.GetUserId(),
                450,
                "ActorUserId"),
            entry.ActionCode,
            targetEntityType,
            targetEntityId,
            NormalizeIdentifier(
                entry.JobPostingId,
                128,
                nameof(entry.JobPostingId)),
            NormalizeIdentifier(
                entry.CandidateId,
                450,
                nameof(entry.CandidateId)),
            redactor.Redact(entry.Summary));

        dbContext.ActivityLogs.Add(activityLog);

        return activityLog;
    }

    private string? NormalizeTargetEntityType(string? targetEntityType)
    {
        var normalized = NormalizeIdentifier(
            targetEntityType,
            100,
            nameof(ActivityLogEntry.TargetEntityType));

        if (normalized is not null && !ActivityEntityTypes.IsDefined(normalized))
        {
            throw new ArgumentException(
                "Hedef varlık türü merkezi listede tanımlı olmalıdır.",
                nameof(targetEntityType));
        }

        return normalized;
    }

    private string? NormalizeIdentifier(
        string? value,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"Değer en fazla {maximumLength} karakter olabilir.",
                parameterName);
        }

        if (normalized.Any(
                character =>
                    char.IsControl(character) ||
                    char.GetUnicodeCategory(character) is
                        UnicodeCategory.Format or
                        UnicodeCategory.LineSeparator or
                        UnicodeCategory.ParagraphSeparator))
        {
            throw new ArgumentException(
                "Değer kontrol veya görünmez yönlendirme karakteri içeremez.",
                parameterName);
        }

        if (!string.Equals(
                redactor.Redact(normalized),
                normalized,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Değer hassas veya güvenli olmayan içerik barındıramaz.",
                parameterName);
        }

        return normalized;
    }
}
