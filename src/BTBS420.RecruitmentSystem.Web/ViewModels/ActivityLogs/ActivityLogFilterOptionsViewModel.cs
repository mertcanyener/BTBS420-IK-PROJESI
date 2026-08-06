using BTBS420.RecruitmentSystem.Web.ViewModels.Positions;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.ActivityLogs;

public sealed record ActivityLogFilterOptionsViewModel(
    IReadOnlyList<TextSelectOptionViewModel> UserOptions,
    IReadOnlyList<SelectOptionViewModel> JobPostingOptions,
    IReadOnlyList<TextSelectOptionViewModel> CandidateOptions,
    IReadOnlyList<string> ActionCodeOptions);
