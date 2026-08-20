namespace BTBS420.RecruitmentSystem.Web.ViewModels.Dashboard;

public sealed record AdminDashboardMetricsViewModel(
    int TotalUsers,
    int ActiveJobPostings,
    int TotalApplications,
    int InProgressApplications,
    int HiredCount);
