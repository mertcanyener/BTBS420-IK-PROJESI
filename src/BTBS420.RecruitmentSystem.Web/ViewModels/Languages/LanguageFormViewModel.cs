using System.ComponentModel.DataAnnotations;
using BTBS420.RecruitmentSystem.Web.Models;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.Languages;

public sealed class LanguageFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Dil adı zorunludur.")]
    [StringLength(
        Language.MaximumNameLength,
        ErrorMessage = "Dil adı en fazla {1} karakter olabilir.")]
    [Display(Name = "Dil adı")]
    public string Name { get; set; } = string.Empty;
}
