using System.ComponentModel.DataAnnotations;
using BTBS420.RecruitmentSystem.Web.Models;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.ExperienceRanges;

public sealed class ExperienceRangeFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Deneyim aralığı adı zorunludur.")]
    [StringLength(
        ExperienceRange.MaximumNameLength,
        ErrorMessage = "Deneyim aralığı adı en fazla {1} karakter olabilir.")]
    [Display(Name = "Deneyim aralığı adı")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Minimum yıl zorunludur.")]
    [Range(0, int.MaxValue, ErrorMessage = "Minimum yıl negatif olamaz.")]
    [Display(Name = "Minimum yıl")]
    public int? MinYears { get; set; }

    [Required(ErrorMessage = "Maksimum yıl zorunludur.")]
    [Range(0, int.MaxValue, ErrorMessage = "Maksimum yıl negatif olamaz.")]
    [Display(Name = "Maksimum yıl")]
    public int? MaxYears { get; set; }
}
