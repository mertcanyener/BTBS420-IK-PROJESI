using System.ComponentModel.DataAnnotations;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.Positions;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.CandidateEducations;

public sealed class CandidateEducationFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Eğitim seviyesi seçimi zorunludur.")]
    [Display(Name = "Eğitim Seviyesi")]
    public int? EducationId { get; set; }

    [Required(ErrorMessage = "Okul adı zorunludur.")]
    [StringLength(
        CandidateEducation.MaximumSchoolNameLength,
        ErrorMessage = "Okul adı en fazla {1} karakter olabilir.")]
    [Display(Name = "Okul Adı")]
    public string SchoolName { get; set; } = string.Empty;

    [StringLength(
        CandidateEducation.MaximumFieldOfStudyLength,
        ErrorMessage = "Bölüm en fazla {1} karakter olabilir.")]
    [Display(Name = "Bölüm")]
    public string? FieldOfStudy { get; set; }

    [Required(ErrorMessage = "Başlangıç tarihi zorunludur.")]
    [DataType(DataType.Date)]
    [Display(Name = "Başlangıç Tarihi")]
    public DateOnly? StartDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Bitiş Tarihi")]
    public DateOnly? EndDate { get; set; }

    [Display(Name = "Hâlâ devam ediyor")]
    public bool IsOngoing { get; set; }

    public IReadOnlyList<SelectOptionViewModel> EducationOptions { get; set; } = [];
}
