using System.ComponentModel.DataAnnotations;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.Positions;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.CandidateProfiles;

public sealed class CandidateProfileFormViewModel
{
    [Required(ErrorMessage = "Ad zorunludur.")]
    [StringLength(
        CandidateProfile.MaximumFirstNameLength,
        ErrorMessage = "Ad en fazla {1} karakter olabilir.")]
    [Display(Name = "Ad")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Soyad zorunludur.")]
    [StringLength(
        CandidateProfile.MaximumLastNameLength,
        ErrorMessage = "Soyad en fazla {1} karakter olabilir.")]
    [Display(Name = "Soyad")]
    public string LastName { get; set; } = string.Empty;

    [StringLength(
        CandidateProfile.MaximumProfessionalSummaryLength,
        ErrorMessage = "Mesleki özet en fazla {1} karakter olabilir.")]
    [Display(Name = "Mesleki Özet")]
    public string? ProfessionalSummary { get; set; }

    [Display(Name = "Hedef Pozisyon")]
    public int? TargetPositionId { get; set; }

    [Display(Name = "Yetkinlikler")]
    public List<int> SelectedSkillIds { get; set; } = [];

    [Display(Name = "Diller")]
    public List<int> SelectedLanguageIds { get; set; } = [];

    public IReadOnlyList<SelectOptionViewModel> PositionOptions { get; set; } = [];

    public IReadOnlyList<SelectOptionViewModel> SkillOptions { get; set; } = [];

    public IReadOnlyList<SelectOptionViewModel> LanguageOptions { get; set; } = [];
}
