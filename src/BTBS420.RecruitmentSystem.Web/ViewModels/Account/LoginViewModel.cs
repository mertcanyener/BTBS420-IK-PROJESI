using System.ComponentModel.DataAnnotations;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.Account;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Kullanıcı adı veya e-posta zorunludur.")]
    [Display(Name = "Kullanıcı adı veya e-posta")]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola zorunludur.")]
    [DataType(DataType.Password)]
    [Display(Name = "Parola")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
