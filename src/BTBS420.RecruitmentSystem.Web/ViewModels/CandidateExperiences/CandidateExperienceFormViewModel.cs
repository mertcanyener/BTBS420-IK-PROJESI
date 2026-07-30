using System.ComponentModel.DataAnnotations;
using BTBS420.RecruitmentSystem.Web.Models;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.CandidateExperiences;

public sealed class CandidateExperienceFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Şirket adı zorunludur.")]
    [StringLength(
        CandidateExperience.MaximumCompanyNameLength,
        ErrorMessage = "Şirket adı en fazla {1} karakter olabilir.")]
    [Display(Name = "Şirket Adı")]
    public string CompanyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Unvan zorunludur.")]
    [StringLength(
        CandidateExperience.MaximumJobTitleLength,
        ErrorMessage = "Unvan en fazla {1} karakter olabilir.")]
    [Display(Name = "Unvan")]
    public string JobTitle { get; set; } = string.Empty;

    [Required(ErrorMessage = "Başlangıç tarihi zorunludur.")]
    [DataType(DataType.Date)]
    [Display(Name = "Başlangıç Tarihi")]
    public DateOnly? StartDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Bitiş Tarihi")]
    public DateOnly? EndDate { get; set; }

    [Display(Name = "Hâlâ devam ediyor")]
    public bool IsOngoing { get; set; }
}
