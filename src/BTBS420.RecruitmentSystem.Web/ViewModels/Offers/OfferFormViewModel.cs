using System.ComponentModel.DataAnnotations;
using BTBS420.RecruitmentSystem.Web.Models;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.Offers;

public sealed class OfferFormViewModel
{
    public int? OfferId { get; set; }

    public int JobApplicationId { get; set; }

    public string CandidateName { get; set; } = string.Empty;

    public string JobPostingTitle { get; set; } = string.Empty;

    public string Status { get; set; } = OfferStatuses.Draft;

    public string StatusLabel { get; set; } = OfferStatuses.GetDisplayLabel(OfferStatuses.Draft);

    public bool CanEdit { get; set; } = true;

    public bool CanSubmit { get; set; }

    public bool CanDecide { get; set; }

    [Display(Name = "Maaş")]
    [Range(
        typeof(decimal),
        "0.01",
        "79228162514264337593543950335",
        ErrorMessage = "Maaş sıfırdan büyük olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? Salary { get; set; }

    [Display(Name = "Başlangıç Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly? StartDate { get; set; }

    [Display(Name = "Not")]
    [MaxLength(Offer.MaximumNoteLength)]
    public string? Note { get; set; }

    public string? RowVersion { get; set; }
}
