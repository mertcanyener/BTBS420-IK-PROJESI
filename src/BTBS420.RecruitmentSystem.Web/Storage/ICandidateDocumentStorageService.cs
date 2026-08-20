namespace BTBS420.RecruitmentSystem.Web.Storage;

public interface ICandidateDocumentStorageService
{
    Task SaveAsync(
        int candidateProfileId,
        string storedFileName,
        Stream content,
        CancellationToken cancellationToken);

    Stream OpenRead(int candidateProfileId, string storedFileName);

    void Delete(int candidateProfileId, string storedFileName);
}
