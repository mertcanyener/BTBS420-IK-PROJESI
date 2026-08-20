namespace BTBS420.RecruitmentSystem.Web.ViewModels.Dashboard;

public sealed record ManagerDashboardMetricsViewModel(
    int OpenPositions,
    int NewCount,
    int ScreeningCount,
    int InterviewCount,
    int HiredCount,
    int RejectedCount,
    int WithdrawnCount);
