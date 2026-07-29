using System.ComponentModel.DataAnnotations;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.Positions;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.JobPostings;

public sealed class JobPostingFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "İlan başlığı zorunludur.")]
    [StringLength(
        JobPosting.MaximumTitleLength,
        ErrorMessage = "İlan başlığı en fazla {1} karakter olabilir.")]
    [Display(Name = "İlan Başlığı")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "İlan açıklaması zorunludur.")]
    [StringLength(
        JobPosting.MaximumDescriptionLength,
        ErrorMessage = "İlan açıklaması en fazla {1} karakter olabilir.")]
    [Display(Name = "Açıklama")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Pozisyon seçimi zorunludur.")]
    [Display(Name = "Pozisyon")]
    public int? PositionId { get; set; }

    [Required(ErrorMessage = "Sorumlu uzman seçimi zorunludur.")]
    [Display(Name = "Sorumlu Uzman")]
    public string? ResponsibleUserId { get; set; }

    [Required(ErrorMessage = "Son başvuru tarihi zorunludur.")]
    [DataType(DataType.Date)]
    [Display(Name = "Son Başvuru Tarihi")]
    public DateOnly? ApplicationDeadline { get; set; }

    [Display(Name = "Şirket İçi İlan")]
    public bool IsInternal { get; set; }

    public string? RowVersion { get; set; }

    public IReadOnlyList<SelectOptionViewModel> PositionOptions { get; set; } = [];

    public IReadOnlyList<UserOptionViewModel> ResponsibleUserOptions { get; set; } = [];
}
