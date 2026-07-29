using System.ComponentModel.DataAnnotations;
using BTBS420.RecruitmentSystem.Web.Models;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.Locations;

public sealed class LocationFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Konum adı zorunludur.")]
    [StringLength(
        Location.MaximumNameLength,
        ErrorMessage = "Konum adı en fazla {1} karakter olabilir.")]
    [Display(Name = "Konum adı")]
    public string Name { get; set; } = string.Empty;
}
