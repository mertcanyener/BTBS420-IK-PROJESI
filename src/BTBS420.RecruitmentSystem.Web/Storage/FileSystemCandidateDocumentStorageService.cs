using System.Globalization;
using Microsoft.Extensions.Options;

namespace BTBS420.RecruitmentSystem.Web.Storage;

public sealed class FileSystemCandidateDocumentStorageService(
    IWebHostEnvironment environment,
    IOptions<CandidateDocumentStorageOptions> storageOptions) : ICandidateDocumentStorageService
{
    public async Task SaveAsync(
        int candidateProfileId,
        string storedFileName,
        Stream content,
        CancellationToken cancellationToken)
    {
        var directory = ResolveCandidateDirectory(candidateProfileId);
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, storedFileName);

        await using var fileStream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        await content.CopyToAsync(fileStream, cancellationToken);
    }

    public Stream OpenRead(int candidateProfileId, string storedFileName)
    {
        var path = Path.Combine(ResolveCandidateDirectory(candidateProfileId), storedFileName);

        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public void Delete(int candidateProfileId, string storedFileName)
    {
        var path = Path.Combine(ResolveCandidateDirectory(candidateProfileId), storedFileName);

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string ResolveCandidateDirectory(int candidateProfileId)
    {
        var rootPath = storageOptions.Value.RootPath;
        var resolvedRoot = Path.IsPathRooted(rootPath)
            ? rootPath
            : Path.Combine(environment.ContentRootPath, rootPath);

        return Path.Combine(
            resolvedRoot,
            candidateProfileId.ToString(CultureInfo.InvariantCulture));
    }
}
