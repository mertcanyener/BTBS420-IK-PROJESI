namespace BTBS420.RecruitmentSystem.Web.ViewModels.Interviews;

public sealed class InterviewIndexViewModel(IReadOnlyList<InterviewListItemViewModel> interviews)
{
    public IReadOnlyList<InterviewListItemViewModel> Interviews { get; } =
        interviews ?? throw new ArgumentNullException(nameof(interviews));
}
