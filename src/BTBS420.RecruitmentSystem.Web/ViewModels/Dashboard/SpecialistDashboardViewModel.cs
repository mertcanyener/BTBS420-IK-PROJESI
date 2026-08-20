using BTBS420.RecruitmentSystem.Web.ViewModels.ApplicationsPool;
using BTBS420.RecruitmentSystem.Web.ViewModels.Interviews;
using BTBS420.RecruitmentSystem.Web.ViewModels.JobPostings;
using BTBS420.RecruitmentSystem.Web.ViewModels.Offers;

namespace BTBS420.RecruitmentSystem.Web.ViewModels.Dashboard;

public sealed record SpecialistDashboardViewModel(
    IReadOnlyList<JobPostingListItemViewModel> JobPostings,
    IReadOnlyList<ApplicationPoolListItemViewModel> CandidatePool,
    IReadOnlyList<InterviewListItemViewModel> UpcomingInterviews,
    IReadOnlyList<OfferListItemViewModel> PendingOffers,
    SpecialistDashboardFilterViewModel Filter,
    DashboardFilterOptionsViewModel FilterOptions);
