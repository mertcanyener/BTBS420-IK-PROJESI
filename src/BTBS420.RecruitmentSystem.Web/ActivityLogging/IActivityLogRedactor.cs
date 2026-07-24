namespace BTBS420.RecruitmentSystem.Web.ActivityLogging;

public interface IActivityLogRedactor
{
    string Redact(string? summary);
}
