using System.ComponentModel.DataAnnotations;
using BTBS420.RecruitmentSystem.Web.Models;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.Offers;

public sealed class OfferFormViewModel
{
    public int? OfferId { get; set; }

    public int JobApplicationId { get; set; }

    public string CandidateName { get; set; } = string.Empty;

    public string JobPostingTitle { get; set; } = string.Empty;

    [Display(Name = "Maaş")]
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "Maaş sıfırdan büyük olmalıdır.")]
    public decimal? Salary { get; set; }

    [Display(Name = "Başlangıç Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly? StartDate { get; set; }

    [Display(Name = "Not")]
    [MaxLength(Offer.MaximumNoteLength)]
    public string? Note { get; set; }

    public string? RowVersion { get; set; }
}
