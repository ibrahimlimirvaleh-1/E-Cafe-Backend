using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.File;
using ECafe.Application.Repositories.File;
using ECafe.Application.Services;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Features.Queries.File
{
    public class DownloadFileQueryHandler : BaseManager, IRequestHandler<DownloadFileQuery, GetFileResponse>
    {
        private readonly IFileRepository _fileRepository;
        private readonly IMinioService _minioService;

        public DownloadFileQueryHandler(
            IHttpContextAccessor httpContextAccessor,
            IMapper mapper,
            IConfiguration configuration,
            IFileRepository fileRepository,
            IMinioService minioService)
            : base(httpContextAccessor, mapper, configuration)
        {
            _fileRepository = fileRepository;
            _minioService = minioService;
        }

        public async Task<GetFileResponse> Handle(DownloadFileQuery request, CancellationToken cancellationToken)
        {
            if (request.FileId <= 0)
                throw new BadRequestException(ErrorCode.BadRequest);

            var file = await _fileRepository.GetWithUsageByIdAsync(request.FileId);
            if (file is null)
                throw new NotFoundException(ErrorCode.FileNotFound);

            EnsureCurrentUserCanAccessFile(file);
            return await _minioService.GetFileAsync(file.Token);
        }

        private void EnsureCurrentUserCanAccessFile(Domain.Entities.File file)
        {
            var contractRestaurantIds = file.RestaurantContracts
                .Select(contract => contract.RestaurantId)
                .Distinct()
                .ToList();

            if (contractRestaurantIds.Count > 0)
            {
                foreach (var restaurantId in contractRestaurantIds)
                    EnsureCurrentUserCanAccessRestaurant(restaurantId);

                return;
            }

            if (file.Restaurant is not null)
            {
                EnsureCurrentUserCanAccessRestaurant(file.Restaurant.Id);
                return;
            }

            var itemRestaurantIds = file.Items
                .Select(item => item.RestaurantId)
                .Distinct()
                .ToList();

            if (itemRestaurantIds.Count > 0)
            {
                foreach (var restaurantId in itemRestaurantIds)
                    EnsureCurrentUserCanAccessRestaurant(restaurantId);

                return;
            }

            if (file.User is not null)
            {
                if (!IsCurrentUserSuperAdmin() && file.User.Id != GetCurrentUserId())
                    throw new ForbiddenException(ErrorCode.AccessDenied);

                return;
            }

            throw new NotFoundException(ErrorCode.FileNotFound);
        }
    }
}
