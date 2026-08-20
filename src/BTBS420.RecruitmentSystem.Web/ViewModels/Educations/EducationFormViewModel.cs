using System.ComponentModel.DataAnnotations;
using BTBS420.RecruitmentSystem.Web.Models;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.Educations;

public sealed class EducationFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Eğitim adı zorunludur.")]
    [StringLength(
        Education.MaximumNameLength,
        ErrorMessage = "Eğitim adı en fazla {1} karakter olabilir.")]
    [Display(Name = "Eğitim adı")]
    public string Name { get; set; } = string.Empty;
}
