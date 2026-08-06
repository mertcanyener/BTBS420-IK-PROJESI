namespace BTBS420.RecruitmentSystem.Web.ViewModels.Dashboard;

public sealed record AdminDashboardFilterViewModel(
    int? DepartmentId,
    int? PositionId,
    int? JobPostingId,
    DateOnly? DateFrom,
    DateOnly? DateTo);
