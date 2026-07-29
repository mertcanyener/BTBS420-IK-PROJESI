namespace BTBS420.RecruitmentSystem.Web.ViewModels.Positions;

public sealed class PositionIndexViewModel(
    IReadOnlyList<PositionListItemViewModel> positions)
{
    public IReadOnlyList<PositionListItemViewModel> Positions { get; } =
        positions ?? throw new ArgumentNullException(nameof(positions));
}
