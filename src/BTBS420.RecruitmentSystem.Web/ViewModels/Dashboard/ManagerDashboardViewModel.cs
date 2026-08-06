using BTBS420.RecruitmentSystem.Web.ViewModels.ApplicationsPool;
using BTBS420.RecruitmentSystem.Web.ViewModels.Interviews;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.Dashboard;

public sealed record ManagerDashboardViewModel(
    ManagerDashboardMetricsViewModel Metrics,
    IReadOnlyList<ApplicationPoolListItemViewModel> Shortlist,
    IReadOnlyList<PendingEvaluationListItemViewModel> PendingEvaluations,
    ManagerDashboardFilterViewModel Filter,
    ManagerDashboardFilterOptionsViewModel FilterOptions);
