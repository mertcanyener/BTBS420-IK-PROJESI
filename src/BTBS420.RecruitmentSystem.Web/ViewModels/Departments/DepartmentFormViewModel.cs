using System.ComponentModel.DataAnnotations;
using BTBS420.RecruitmentSystem.Web.Models;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.Departments;

public sealed class DepartmentFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Departman adı zorunludur.")]
    [StringLength(
        Department.MaximumNameLength,
        ErrorMessage = "Departman adı en fazla {1} karakter olabilir.")]
    [Display(Name = "Departman adı")]
    public string Name { get; set; } = string.Empty;
}
