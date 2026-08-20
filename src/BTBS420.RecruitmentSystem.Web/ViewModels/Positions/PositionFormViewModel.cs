using System.ComponentModel.DataAnnotations;
using BTBS420.RecruitmentSystem.Web.Models;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.Positions;

public sealed class PositionFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Pozisyon adı zorunludur.")]
    [StringLength(
        Position.MaximumNameLength,
        ErrorMessage = "Pozisyon adı en fazla {1} karakter olabilir.")]
    [Display(Name = "Pozisyon adı")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Departman seçimi zorunludur.")]
    [Display(Name = "Departman")]
    public int? DepartmentId { get; set; }

    [Display(Name = "İş Ailesi")]
    public int? JobFamilyId { get; set; }

    [Display(Name = "Kıdem")]
    public int? SeniorityId { get; set; }

    public IReadOnlyList<SelectOptionViewModel> DepartmentOptions { get; set; } = [];

    public IReadOnlyList<SelectOptionViewModel> JobFamilyOptions { get; set; } = [];

    public IReadOnlyList<SelectOptionViewModel> SeniorityOptions { get; set; } = [];
}
