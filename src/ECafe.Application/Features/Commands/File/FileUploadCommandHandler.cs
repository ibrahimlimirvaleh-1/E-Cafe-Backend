using AutoMapper;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.DTOs.File;
using ECafe.Application.Repositories.File;
using ECafe.Application.Services.MinIO.Abstracts;
using MediatR;

namespace ECafe.Application.Features.Commands.File
{
    public class FileUploadCommandHandler : IRequestHandler<FileUploadCommand, FileResponse>
    {
        private readonly IMinioService _minioService;
        private readonly IFileRepository _fileRepository;
        private readonly IMapper _mapper;

        public FileUploadCommandHandler(
            IMinioService minioService,
            IFileRepository fileRepository,
            IMapper mapper)
        {
            _minioService = minioService;
            _fileRepository = fileRepository;
            _mapper = mapper;
        }

        public async Task<FileResponse> Handle(FileUploadCommand request, CancellationToken cancellationToken)
        {
            var file = request.File;
            if (file is null || file.Length == 0)
                throw new ArgumentException("File is required.", nameof(request));

            var token = await _minioService.UploadFileAsync(new UploadFileDto(file));
            var url = await _minioService.GenerateFileUrl(token);
            var fileEntity = _mapper.Map<Domain.Entities.File>(new FileMapData
            {
                Token = token,
                FileName = file.FileName,
                Size = file.Length,
                Url = url
            });

            await _fileRepository.Add(fileEntity);
            await _fileRepository.SaveChangesAsync();

            return new FileResponse
            {
                Id = fileEntity.Id,
                Name = fileEntity.Name,
                Extension = fileEntity.Extension,
                Size = fileEntity.Size,
                Url = url,
                IsAttached = false
            };
        }
    }
}
