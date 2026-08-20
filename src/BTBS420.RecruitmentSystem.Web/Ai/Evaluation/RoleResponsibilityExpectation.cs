namespace BTBS420.RecruitmentSystem.Web.Ai.Evaluation;

public enum ResponsibilityCategory
{
    Technical,
    Managerial,
    Leadership,
    Domain,
    Business,
}

public sealed record RoleResponsibilityExpectation(
    ResponsibilityCategory Category,
    string Description,
    int ImportanceRank);
