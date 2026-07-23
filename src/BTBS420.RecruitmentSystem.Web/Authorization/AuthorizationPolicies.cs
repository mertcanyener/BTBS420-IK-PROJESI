using Microsoft.AspNetCore.Authorization;

namespace BTBS420.RecruitmentSystem.Web.Authorization;

public static class AuthorizationPolicies
{
    public const string AdminOnly = "Identity.AdminOnly";
    public const string RecruitmentSpecialistOnly = "Identity.RecruitmentSpecialistOnly";
    public const string HiringManagerOnly = "Identity.HiringManagerOnly";
    public const string CandidateOnly = "Identity.CandidateOnly";

    public static void Configure(AuthorizationOptions options)
    {
        AddRolePolicy(options, AdminOnly, SystemRoles.Admin);
        AddRolePolicy(
            options,
            RecruitmentSpecialistOnly,
            SystemRoles.RecruitmentSpecialist);
        AddRolePolicy(options, HiringManagerOnly, SystemRoles.HiringManager);
        AddRolePolicy(options, CandidateOnly, SystemRoles.Candidate);
    }

    private static void AddRolePolicy(
        AuthorizationOptions options,
        string policyName,
        string roleName)
    {
        options.AddPolicy(
            policyName,
            policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(roleName));
    }
}
