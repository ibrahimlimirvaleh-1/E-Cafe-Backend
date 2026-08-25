using ECafe.Application.Repositories.File;
using ECafe.Application.Services.FileAccess.Abstract;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Domain.Exceptions;
using MediatR;

namespace ECafe.Application.Features.Commands.File
{
    public class DeleteFileCommandHandler : IRequestHandler<DeleteFileCommand>
    {
        private readonly IFileRepository _fileRepository;
        private readonly IMinioService _minioService;
        private readonly IFileAccessPolicy _fileAccessPolicy;

        public DeleteFileCommandHandler(
            IFileRepository fileRepository,
            IMinioService minioService,
            IFileAccessPolicy fileAccessPolicy)
        {
            _fileRepository = fileRepository;
            _minioService = minioService;
            _fileAccessPolicy = fileAccessPolicy;
        }

        public async Task Handle(DeleteFileCommand request, CancellationToken cancellationToken)
        {
            if (request.FileId <= 0)
                throw new BusinessRuleException(ErrorCode.InvalidFileId);

            var file = await _fileRepository.GetWithUsageByIdAsync(request.FileId);
            if (file is null)
                throw new BusinessRuleException(ErrorCode.FileNotFound);

            _fileAccessPolicy.EnsureCurrentUserCanAccess(file);

            if (await _fileRepository.IsAttachedAsync(request.FileId))
                throw new BusinessRuleException(ErrorCode.AttachedFileCannotBeDeleted);

            await _minioService.DeleteFileAsync(file.Token);
            await _fileRepository.Delete(file);
            await _fileRepository.SaveChangesAsync();
        }
    }
}
