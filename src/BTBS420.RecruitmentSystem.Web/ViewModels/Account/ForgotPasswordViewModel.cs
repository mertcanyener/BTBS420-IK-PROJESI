using System.ComponentModel.DataAnnotations;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.Account;

public sealed class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;
}
