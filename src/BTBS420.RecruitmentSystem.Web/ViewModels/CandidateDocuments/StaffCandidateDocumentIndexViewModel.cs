namespace BTBS420.RecruitmentSystem.Web.ViewModels.CandidateDocuments;

public sealed class StaffCandidateDocumentIndexViewModel(
    int candidateProfileId,
    string candidateFullName,
    IReadOnlyList<CandidateDocumentListItemViewModel> documents)
{
    public int CandidateProfileId { get; } = candidateProfileId;

    public string CandidateFullName { get; } =
        candidateFullName ?? throw new ArgumentNullException(nameof(candidateFullName));

    public IReadOnlyList<CandidateDocumentListItemViewModel> Documents { get; } =
        documents ?? throw new ArgumentNullException(nameof(documents));
}
