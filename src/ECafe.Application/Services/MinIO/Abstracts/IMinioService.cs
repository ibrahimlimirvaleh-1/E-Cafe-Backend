using ECafe.Application.DTOs.File;

namespace ECafe.Application.Services.MinIO.Abstracts
{
    public interface IMinioService
    {
        public Task<string> UploadFileAsync(UploadFileDto request);

        public Task<string> UploadFileAsync(UploadFileDto request, FileUploadPolicy policy);

        public Task<string> UploadFileAsync(UploadGeneratedFileDto request);

        public Task<string> UploadFileAsync(UploadGeneratedFileDto request, FileUploadPolicy policy);

        Task<GetFileResponse> GetFileAsync(string token);

        Task DeleteFileAsync(string token);

        public Task<string> GenerateFileUrl(string token);

    }
}
