using System.ComponentModel.DataAnnotations;
using BTBS420.RecruitmentSystem.Web.Models;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.JobFamilies;

public sealed class JobFamilyFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "İş ailesi adı zorunludur.")]
    [StringLength(
        JobFamily.MaximumNameLength,
        ErrorMessage = "İş ailesi adı en fazla {1} karakter olabilir.")]
    [Display(Name = "İş ailesi adı")]
    public string Name { get; set; } = string.Empty;
}
