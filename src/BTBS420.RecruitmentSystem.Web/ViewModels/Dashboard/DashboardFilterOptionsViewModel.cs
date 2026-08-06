using BTBS420.RecruitmentSystem.Web.ViewModels.Positions;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.Dashboard;

public sealed record DashboardFilterOptionsViewModel(
    IReadOnlyList<string> StatusOptions,
    IReadOnlyList<SelectOptionViewModel> DepartmentOptions,
    IReadOnlyList<SelectOptionViewModel> PositionOptions,
    IReadOnlyList<SelectOptionViewModel> JobPostingOptions);
