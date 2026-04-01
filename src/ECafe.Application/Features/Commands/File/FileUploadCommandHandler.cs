using ECafe.Application.DTOs.File;
using ECafe.Application.Services.MinIO.Abstracts;
using MediatR;

namespace ECafe.Application.Features.Commands.File
{
    public class FileUploadCommandHandler : IRequestHandler<FileUploadCommand, string>
    {
        private readonly IMinioService _minioService;

        public FileUploadCommandHandler(IMinioService minioService)
        {
            _minioService = minioService;
        }

        public async Task<string> Handle(FileUploadCommand request, CancellationToken cancellationToken)
        {
            var dto = new UploadFileDto(request.File);
            return await _minioService.UploadFileAsync(dto);
        }
    }
}
