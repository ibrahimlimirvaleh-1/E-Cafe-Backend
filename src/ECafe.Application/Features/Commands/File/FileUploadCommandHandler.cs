using AutoMapper;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.DTOs.File;
using ECafe.Application.Repositories.File;
using ECafe.Application.Services.MinIO.Abstracts;
using MediatR;

namespace ECafe.Application.Features.Commands.File
{
    public class FileUploadCommandHandler : IRequestHandler<FileUploadCommand, int>
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

        public async Task<int> Handle(FileUploadCommand request, CancellationToken cancellationToken)
        {
            var file = request.File;
            if (file is null || file.Length == 0)
                throw new ArgumentException("File is required.", nameof(request));

            var token = await _minioService.UploadFileAsync(new UploadFileDto(file));
            var fileEntity = _mapper.Map<Domain.Entities.File>(new FileMapData
            {
                Token = token,
                FileName = file.FileName,
                Size = file.Length,
                Url = await _minioService.GenerateFileUrl(token)
            });

            await _fileRepository.Add(fileEntity);
            await _fileRepository.SaveChangesAsync();

            return fileEntity.Id;
        }
    }
}
