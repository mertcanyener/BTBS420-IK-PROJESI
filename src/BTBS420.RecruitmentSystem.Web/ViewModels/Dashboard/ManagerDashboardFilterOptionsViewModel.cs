using BTBS420.RecruitmentSystem.Web.ViewModels.Positions;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.Dashboard;

public sealed record ManagerDashboardFilterOptionsViewModel(
    IReadOnlyList<SelectOptionViewModel> PositionOptions,
    IReadOnlyList<SelectOptionViewModel> JobPostingOptions);
