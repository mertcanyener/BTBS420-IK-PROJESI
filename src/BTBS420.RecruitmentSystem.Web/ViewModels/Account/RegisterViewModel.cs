using System.ComponentModel.DataAnnotations;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.Account;

public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
    [Display(Name = "Kullanıcı adı")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola zorunludur.")]
    [DataType(DataType.Password)]
    [Display(Name = "Parola")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola tekrarı zorunludur.")]
    [DataType(DataType.Password)]
    [Display(Name = "Parola tekrar")]
    [Compare(nameof(Password), ErrorMessage = "Parolalar eşleşmiyor.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
