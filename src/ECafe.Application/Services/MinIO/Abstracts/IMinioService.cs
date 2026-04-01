using ECafe.Application.DTOs.File;

namespace ECafe.Application.Services.MinIO.Abstracts
{
    public interface IMinioService
    {
        public Task<string> UploadFileAsync(UploadFileDto request);

        Task<GetFileResponse> GetFileAsync(string token);

        public Task<string> GenerateFileUrl(string token);

    }
}
