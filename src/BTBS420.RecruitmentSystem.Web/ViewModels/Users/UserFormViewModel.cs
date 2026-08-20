using System.ComponentModel.DataAnnotations;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.Users;

public sealed class UserFormViewModel
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
    [Display(Name = "Kullanıcı adı")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Rol zorunludur.")]
    [Display(Name = "Rol")]
    public string Role { get; set; } = string.Empty;

    [Required(ErrorMessage = "Departman zorunludur.")]
    [Display(Name = "Departman")]
    public int? DepartmentId { get; set; }

    public IReadOnlyList<string> RoleOptions { get; set; } = [];

    public IReadOnlyList<DepartmentOptionViewModel> DepartmentOptions { get; set; } = [];
}
