namespace BTBS420.RecruitmentSystem.Web.ViewModels.Offers;

public sealed record OfferListItemViewModel(
    int Id,
    string CandidateFullName,
    string JobPostingTitle,
    decimal Salary,
    DateOnly StartDate,
    DateTime CreatedAtUtc);
