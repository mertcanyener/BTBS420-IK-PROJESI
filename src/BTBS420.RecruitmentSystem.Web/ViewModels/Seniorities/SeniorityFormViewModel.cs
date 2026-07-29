using System.ComponentModel.DataAnnotations;
using BTBS420.RecruitmentSystem.Web.Models;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.Seniorities;

public sealed class SeniorityFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Kıdem adı zorunludur.")]
    [StringLength(
        Seniority.MaximumNameLength,
        ErrorMessage = "Kıdem adı en fazla {1} karakter olabilir.")]
    [Display(Name = "Kıdem adı")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sıra numarası zorunludur.")]
    [Display(Name = "Sıra")]
    public int? Rank { get; set; }
}
