namespace BTBS420.RecruitmentSystem.Web.ViewModels.JobApplications;

public sealed record JobApplicationListItemViewModel(
    int Id,
    int JobPostingId,
    string JobPostingTitle,
    string PositionName,
    string JobPostingStatus,
    string ApplicationStatusLabel,
    DateTime AppliedAtUtc,
    DateTime? WithdrawnAtUtc,
    bool CanWithdraw,
    int? OfferId,
    string? OfferStatusLabel,
    decimal? OfferSalary,
    DateOnly? OfferStartDate,
    string? OfferNote,
    bool CanDecideOffer);
