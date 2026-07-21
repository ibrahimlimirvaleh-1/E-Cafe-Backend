using ECafe.Application.DTOs.File;
using ECafe.Application.Repositories.File;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Domain.Exceptions;
using MediatR;

namespace ECafe.Application.Features.Queries.File
{
    public class GetFileMetadataQueryHandler : IRequestHandler<GetFileMetadataQuery, FileResponse>
    {
        private readonly IFileRepository _fileRepository;
        private readonly IMinioService _minioService;

        public GetFileMetadataQueryHandler(
            IFileRepository fileRepository,
            IMinioService minioService)
        {
            _fileRepository = fileRepository;
            _minioService = minioService;
        }

        public async Task<FileResponse> Handle(GetFileMetadataQuery request, CancellationToken cancellationToken)
        {
            if (request.FileId <= 0)
                throw new BusinessRuleException("Invalid file ID!");

            var file = await _fileRepository.GetByIdAsync(request.FileId);
            if (file is null)
                throw new BusinessRuleException("File not found!");

            return new FileResponse
            {
                Id = file.Id,
                Name = file.Name,
                Extension = file.Extension,
                Size = file.Size,
                Url = await _minioService.GenerateFileUrl(file.Token),
                IsAttached = await _fileRepository.IsAttachedAsync(file.Id)
            };
        }
    }
}
