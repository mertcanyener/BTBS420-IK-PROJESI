namespace BTBS420.RecruitmentSystem.Web.ViewModels.Dashboard;

public sealed record ManagerDashboardFilterViewModel(
    int? PositionId,
    int? JobPostingId,
    DateOnly? DateFrom,
    DateOnly? DateTo);
