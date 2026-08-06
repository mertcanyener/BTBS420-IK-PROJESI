namespace BTBS420.RecruitmentSystem.Web.ViewModels.Dashboard;

public sealed record AdminDashboardViewModel(
    AdminDashboardMetricsViewModel Metrics,
    AdminDashboardFilterViewModel Filter,
    AdminDashboardFilterOptionsViewModel FilterOptions);
