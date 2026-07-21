using ECafe.Application.Repositories.File;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Domain.Exceptions;
using MediatR;

namespace ECafe.Application.Features.Commands.File
{
    public class DeleteFileCommandHandler : IRequestHandler<DeleteFileCommand>
    {
        private readonly IFileRepository _fileRepository;
        private readonly IMinioService _minioService;

        public DeleteFileCommandHandler(
            IFileRepository fileRepository,
            IMinioService minioService)
        {
            _fileRepository = fileRepository;
            _minioService = minioService;
        }

        public async Task Handle(DeleteFileCommand request, CancellationToken cancellationToken)
        {
            if (request.FileId <= 0)
                throw new BusinessRuleException("Invalid file ID!");

            var file = await _fileRepository.GetWithUsageByIdAsync(request.FileId);
            if (file is null)
                throw new BusinessRuleException("File not found!");

            if (await _fileRepository.IsAttachedAsync(request.FileId))
                throw new BusinessRuleException("Attached file cannot be deleted.");

            await _minioService.DeleteFileAsync(file.Token);
            await _fileRepository.Delete(file);
            await _fileRepository.SaveChangesAsync();
        }
    }
}
