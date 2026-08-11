using ECafe.Application.DTOs.File;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Repositories.File;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Domain.Exceptions;
using MediatR;

namespace ECafe.Application.Features.Queries.File
{
    public class GetFileQueryHandler : IRequestHandler<GetFileQuery, GetFileResponse>
    {
        private static readonly HashSet<string> PublicImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        private readonly IFileRepository _fileRepository;
        private readonly IMinioService _minioService;

        public GetFileQueryHandler(
            IFileRepository fileRepository,
            IMinioService minioService)
        {
            _fileRepository = fileRepository;
            _minioService = minioService;
        }

        public async Task<GetFileResponse> Handle(GetFileQuery request, CancellationToken cancellationToken)
        {
            var file = await _fileRepository.GetWithUsageByTokenAsync(request.token);
            if (file is null || !PublicImageExtensions.Contains(file.Extension))
                throw new NotFoundException(ErrorCode.FileNotFound);

            return await _minioService.GetFileAsync(file.Token);
        }
    }
}
