namespace BTBS420.RecruitmentSystem.Web.Storage;

public sealed class CandidateDocumentStorageOptions
{
    public const string SectionName = "CandidateDocumentStorage";

    public string RootPath { get; set; } = "App_Data/CandidateDocuments";

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}
