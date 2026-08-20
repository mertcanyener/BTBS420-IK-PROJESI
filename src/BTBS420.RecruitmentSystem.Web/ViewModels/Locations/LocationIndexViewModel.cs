namespace BTBS420.RecruitmentSystem.Web.ViewModels.Locations;

public sealed class LocationIndexViewModel(
    IReadOnlyList<LocationListItemViewModel> locations)
{
    public IReadOnlyList<LocationListItemViewModel> Locations { get; } =
        locations ?? throw new ArgumentNullException(nameof(locations));
}
