namespace BTBS420.RecruitmentSystem.Web.ViewModels.Dashboard;

public sealed record DashboardFilterViewModel(
    string? Status,
    int? DepartmentId,
    int? PositionId,
    int? JobPostingId,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    int Page,
    int PageSize);
