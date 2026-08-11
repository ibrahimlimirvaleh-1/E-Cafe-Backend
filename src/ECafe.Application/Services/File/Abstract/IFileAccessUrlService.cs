namespace ECafe.Application.Services.FileAccess.Abstract
{
    public interface IFileAccessUrlService
    {
        string BuildViewUrl(int fileId);

        string BuildDownloadUrl(int fileId);

        Task<string?> BuildPrimaryUrlAsync(Domain.Entities.File? file, CancellationToken cancellationToken = default);

        Task<(string Url, string? DownloadUrl)> BuildUrlsAsync(
            Domain.Entities.File file,
            CancellationToken cancellationToken = default);
    }
}
