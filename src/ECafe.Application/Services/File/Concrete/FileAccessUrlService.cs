using ECafe.Application.Services.FileAccess.Abstract;
using ECafe.Application.Services.MinIO.Abstracts;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.FileAccess.Concrete
{
    public class FileAccessUrlService : IFileAccessUrlService
    {
        private const string FileIdPlaceholder = "{fileId}";
        private const string DefaultViewPathTemplate = "/api/v1/files/{fileId}/view";
        private const string DefaultDownloadPathTemplate = "/api/v1/files/{fileId}/download";

        private readonly IMinioService _minioService;
        private readonly string _viewPathTemplate;
        private readonly string _downloadPathTemplate;

        public FileAccessUrlService(IMinioService minioService, IConfiguration configuration)
        {
            _minioService = minioService;
            _viewPathTemplate = NormalizeTemplate(
                configuration["FileAccess:PrivateViewPathTemplate"],
                DefaultViewPathTemplate);
            _downloadPathTemplate = NormalizeTemplate(
                configuration["FileAccess:PrivateDownloadPathTemplate"],
                DefaultDownloadPathTemplate);
        }

        public string BuildViewUrl(int fileId)
            => BuildUrl(_viewPathTemplate, fileId);

        public string BuildDownloadUrl(int fileId)
            => BuildUrl(_downloadPathTemplate, fileId);

        public async Task<string?> BuildPrimaryUrlAsync(
            Domain.Entities.File? file,
            CancellationToken cancellationToken = default)
        {
            if (file is null)
                return null;

            if (file.FileType?.IsPublic == true)
                return await _minioService.GenerateFileUrl(file.Token);

            return BuildViewUrl(file.Id);
        }

        public async Task<(string Url, string? DownloadUrl)> BuildUrlsAsync(
            Domain.Entities.File file,
            CancellationToken cancellationToken = default)
        {
            if (file.FileType?.IsPublic == true)
                return (await _minioService.GenerateFileUrl(file.Token), null);

            return (BuildViewUrl(file.Id), BuildDownloadUrl(file.Id));
        }

        private static string NormalizeTemplate(string? configuredTemplate, string defaultTemplate)
        {
            var template = string.IsNullOrWhiteSpace(configuredTemplate)
                ? defaultTemplate
                : configuredTemplate.Trim();

            if (!template.Contains(FileIdPlaceholder, StringComparison.Ordinal))
                template = defaultTemplate;

            return template.StartsWith('/')
                ? template
                : $"/{template}";
        }

        private static string BuildUrl(string template, int fileId)
            => template.Replace(FileIdPlaceholder, fileId.ToString(), StringComparison.Ordinal);
    }
}
