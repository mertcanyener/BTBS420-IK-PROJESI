using System.Collections.Frozen;

namespace BTBS420.RecruitmentSystem.Web.ActivityLogging;

public static class ActivityActionCodes
{
    public const string AuthenticationSucceeded = "authentication.succeeded";
    public const string AuthenticationFailed = "authentication.failed";
    public const string AuthenticationSignedOut = "authentication.signed-out";
    public const string UserRegistered = "user.registered";
    public const string PasswordResetRequested = "password-reset.requested";
    public const string PasswordResetSucceeded = "password-reset.succeeded";
    public const string AuthorizationDenied = "authorization.denied";
    public const string EntityCreated = "entity.created";
    public const string EntityUpdated = "entity.updated";
    public const string EntityStatusChanged = "entity.status-changed";
    public const string EntityArchived = "entity.archived";
    public const string EntityDeleted = "entity.deleted";
    public const string EntityDownloaded = "entity.downloaded";

    private static readonly FrozenSet<string> DefinedCodes =
        new[]
        {
            AuthenticationSucceeded,
            AuthenticationFailed,
            AuthenticationSignedOut,
            UserRegistered,
            PasswordResetRequested,
            PasswordResetSucceeded,
            AuthorizationDenied,
            EntityCreated,
            EntityUpdated,
            EntityStatusChanged,
            EntityArchived,
            EntityDeleted,
            EntityDownloaded
        }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => DefinedCodes;

    public static bool IsDefined(string actionCode)
    {
        return DefinedCodes.Contains(actionCode);
    }

    public static string GetDisplayLabel(string actionCode)
    {
        return actionCode switch
        {
            AuthenticationSucceeded => "Giriş Yapıldı",
            AuthenticationFailed => "Giriş Başarısız",
            AuthenticationSignedOut => "Çıkış Yapıldı",
            UserRegistered => "Kullanıcı Kaydoldu",
            PasswordResetRequested => "Şifre Sıfırlama İstendi",
            PasswordResetSucceeded => "Şifre Sıfırlandı",
            AuthorizationDenied => "Yetkilendirme Reddedildi",
            EntityCreated => "Oluşturuldu",
            EntityUpdated => "Güncellendi",
            EntityStatusChanged => "Durum Değişti",
            EntityArchived => "Arşivlendi",
            EntityDeleted => "Silindi",
            EntityDownloaded => "İndirildi",
            _ => actionCode
        };
    }
}
