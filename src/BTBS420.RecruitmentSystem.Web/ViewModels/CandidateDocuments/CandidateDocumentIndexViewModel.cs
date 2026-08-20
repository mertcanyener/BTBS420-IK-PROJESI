namespace BTBS420.RecruitmentSystem.Web.ViewModels.CandidateDocuments;

public sealed class CandidateDocumentIndexViewModel(
    IReadOnlyList<CandidateDocumentListItemViewModel> documents)
{
    public IReadOnlyList<CandidateDocumentListItemViewModel> Documents { get; } =
        documents ?? throw new ArgumentNullException(nameof(documents));
}
