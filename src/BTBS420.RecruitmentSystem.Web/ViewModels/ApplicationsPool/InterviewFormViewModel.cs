using System.ComponentModel.DataAnnotations;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.ApplicationsPool;

public sealed class InterviewFormViewModel
{
    [Required(ErrorMessage = "Mülakat türü seçimi zorunludur.")]
    [Display(Name = "Mülakat Türü")]
    public string InterviewType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Başlangıç zamanı zorunludur.")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Başlangıç Zamanı")]
    public DateTime? StartAtUtc { get; set; }

    [Required(ErrorMessage = "Bitiş zamanı zorunludur.")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Bitiş Zamanı")]
    public DateTime? EndAtUtc { get; set; }

    [StringLength(
        Models.Interview.MaximumOnlineMeetingLinkLength,
        ErrorMessage = "Toplantı linki en fazla {1} karakter olabilir.")]
    [Display(Name = "Toplantı Linki")]
    public string? OnlineMeetingLink { get; set; }

    [StringLength(
        Models.Interview.MaximumLocationLength,
        ErrorMessage = "Konum en fazla {1} karakter olabilir.")]
    [Display(Name = "Konum")]
    public string? Location { get; set; }

    public IReadOnlyList<InterviewTypeOptionViewModel> InterviewTypeOptions { get; set; } = [];
}
