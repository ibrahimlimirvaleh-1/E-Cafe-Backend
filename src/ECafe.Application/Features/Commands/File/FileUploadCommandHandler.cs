using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.DTOs.File;
using ECafe.Application.Repositories.File;
using ECafe.Application.Repositories.FileType;
using ECafe.Application.Services.FileAccess.Abstract;
using ECafe.Application.Services.ImageProcessing.Abstract;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace ECafe.Application.Features.Commands.File
{
    public class FileUploadCommandHandler : IRequestHandler<FileUploadCommand, FileResponse>
    {
        private readonly IMinioService _minioService;
        private readonly IFileRepository _fileRepository;
        private readonly IFileTypeRepository _fileTypeRepository;
        private readonly IFileAccessUrlService _fileAccessUrlService;
        private readonly IImageProcessingService _imageProcessingService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMapper _mapper;

        public FileUploadCommandHandler(
            IMinioService minioService,
            IFileRepository fileRepository,
            IFileTypeRepository fileTypeRepository,
            IFileAccessUrlService fileAccessUrlService,
            IImageProcessingService imageProcessingService,
            IHttpContextAccessor httpContextAccessor,
            IMapper mapper)
        {
            _minioService = minioService;
            _fileRepository = fileRepository;
            _fileTypeRepository = fileTypeRepository;
            _fileAccessUrlService = fileAccessUrlService;
            _imageProcessingService = imageProcessingService;
            _httpContextAccessor = httpContextAccessor;
            _mapper = mapper;
        }

        public async Task<FileResponse> Handle(FileUploadCommand request, CancellationToken cancellationToken)
        {
            var file = request.File;
            if (file is null || file.Length == 0)
                throw new ArgumentException("File is required.", nameof(request));

            var fileType = await GetFileTypeAsync(request.FileTypeId, cancellationToken);
            if (fileType is null)
                throw new BusinessRuleException(ErrorCode.FileTypeNotFound);

            var uploadPolicy = BuildUploadPolicy(fileType);
            var fileTypeCode = ParseFileTypeCode(fileType.Id);
            var processedFile = await _imageProcessingService.OptimizeForUploadAsync(
                file,
                uploadPolicy,
                fileTypeCode,
                cancellationToken);

            var token = processedFile.IsOptimized
                ? await _minioService.UploadFileAsync(new UploadGeneratedFileDto
                {
                    Bytes = processedFile.Bytes!,
                    FileName = processedFile.FileName,
                    ContentType = processedFile.ContentType,
                    FileTypeId = fileType.Id
                }, uploadPolicy)
                : await _minioService.UploadFileAsync(new UploadFileDto(file), uploadPolicy);

            var url = fileType.IsPublic
                ? await _minioService.GenerateFileUrl(token)
                : string.Empty;

            var fileEntity = _mapper.Map<Domain.Entities.File>(new FileMapData
            {
                Token = token,
                FileName = processedFile.FileName,
                Size = processedFile.Size,
                Url = url,
                FileTypeId = fileType.Id
            });
            fileEntity.CreatedBy = GetCurrentUserId().ToString();

            await _fileRepository.Add(fileEntity);
            await _fileRepository.SaveChangesAsync();

            fileEntity.FileType = fileType;
            var urls = await _fileAccessUrlService.BuildUrlsAsync(fileEntity, cancellationToken);

            return new FileResponse
            {
                Id = fileEntity.Id,
                Name = fileEntity.Name,
                Extension = fileEntity.Extension,
                Size = fileEntity.Size,
                Url = urls.Url,
                DownloadUrl = urls.DownloadUrl,
                FileTypeId = fileType.Id,
                FileTypeCode = fileType.Code,
                IsPublic = fileType.IsPublic,
                IsAttached = false
            };
        }

        private async Task<Domain.Entities.FileType?> GetFileTypeAsync(
            int? fileTypeId,
            CancellationToken cancellationToken)
        {
            if (fileTypeId.HasValue)
                return await _fileTypeRepository.GetByIdAsync(fileTypeId.Value);

            return await _fileTypeRepository.GetByTypeAsync(FileTypeCode.TemporaryUpload, cancellationToken);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("userId")?.Value;
            if (!int.TryParse(userIdClaim, out var userId) || userId <= 0)
                throw new ForbiddenException("User context is required.");

            return userId;
        }

        private static FileUploadPolicy BuildUploadPolicy(Domain.Entities.FileType fileType)
            => new()
            {
                AllowedExtensions = fileType.AllowedExtensions,
                AllowedMimeTypes = fileType.AllowedMimeTypes,
                MaxSizeMb = fileType.MaxSizeMb
            };

        private static FileTypeCode ParseFileTypeCode(int id)
            => Enum.IsDefined(typeof(FileTypeCode), id)
                ? (FileTypeCode)id
                : FileTypeCode.TemporaryUpload;
    }
}
