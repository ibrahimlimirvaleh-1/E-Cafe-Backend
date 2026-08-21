using ECafe.Application.DTOs.File;
using ECafe.Application.Repositories.File;
using ECafe.Application.Services.FileAccess.Abstract;
using ECafe.Domain.Exceptions;
using MediatR;

namespace ECafe.Application.Features.Queries.File
{
    public class GetFileMetadataQueryHandler : IRequestHandler<GetFileMetadataQuery, FileResponse>
    {
        private readonly IFileRepository _fileRepository;
        private readonly IFileAccessUrlService _fileAccessUrlService;

        public GetFileMetadataQueryHandler(
            IFileRepository fileRepository,
            IFileAccessUrlService fileAccessUrlService)
        {
            _fileRepository = fileRepository;
            _fileAccessUrlService = fileAccessUrlService;
        }

        public async Task<FileResponse> Handle(GetFileMetadataQuery request, CancellationToken cancellationToken)
        {
            if (request.FileId <= 0)
                throw new BusinessRuleException(ErrorCode.InvalidFileId);

            var file = await _fileRepository.GetWithUsageByIdAsync(request.FileId);
            if (file is null)
                throw new BusinessRuleException(ErrorCode.FileNotFound);

            var urls = await _fileAccessUrlService.BuildUrlsAsync(file, cancellationToken);

            return new FileResponse
            {
                Id = file.Id,
                Name = file.Name,
                Extension = file.Extension,
                Size = file.Size,
                Url = urls.Url,
                DownloadUrl = urls.DownloadUrl,
                FileTypeId = file.FileTypeId,
                FileTypeCode = file.FileType.Code,
                IsPublic = file.FileType.IsPublic,
                IsAttached = await _fileRepository.IsAttachedAsync(file.Id)
            };
        }
    }
}
