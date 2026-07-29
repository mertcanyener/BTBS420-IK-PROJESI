using System.ComponentModel.DataAnnotations;
using BTBS420.RecruitmentSystem.Web.Models;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.Skills;

public sealed class SkillFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Yetkinlik adı zorunludur.")]
    [StringLength(
        Skill.MaximumNameLength,
        ErrorMessage = "Yetkinlik adı en fazla {1} karakter olabilir.")]
    [Display(Name = "Yetkinlik adı")]
    public string Name { get; set; } = string.Empty;
}
