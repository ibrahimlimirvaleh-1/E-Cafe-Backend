using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.File;
using ECafe.Application.Repositories.File;
using ECafe.Application.Services.FileAccess.Abstract;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Domain.Exceptions;
using MediatR;

namespace ECafe.Application.Features.Queries.File
{
    public class DownloadFileQueryHandler : IRequestHandler<DownloadFileQuery, GetFileResponse>
    {
        private readonly IFileRepository _fileRepository;
        private readonly IMinioService _minioService;
        private readonly IFileAccessPolicy _fileAccessPolicy;

        public DownloadFileQueryHandler(
            IFileRepository fileRepository,
            IMinioService minioService,
            IFileAccessPolicy fileAccessPolicy)
        {
            _fileRepository = fileRepository;
            _minioService = minioService;
            _fileAccessPolicy = fileAccessPolicy;
        }

        public async Task<GetFileResponse> Handle(DownloadFileQuery request, CancellationToken cancellationToken)
        {
            if (request.FileId <= 0)
                throw new BadRequestException(ErrorCode.BadRequest);

            var file = await _fileRepository.GetWithUsageByIdAsync(request.FileId);
            if (file is null)
                throw new NotFoundException(ErrorCode.FileNotFound);

            _fileAccessPolicy.EnsureCurrentUserCanAccess(file);

            var response = await _minioService.GetFileAsync(file.Token);
            response.FileName = $"{file.Name}{file.Extension}";
            return response;
        }
    }
}
