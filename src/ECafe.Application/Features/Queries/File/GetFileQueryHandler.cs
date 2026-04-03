using ECafe.Application.DTOs.File;
using ECafe.Application.Services.MinIO.Abstracts;
using MediatR;

namespace ECafe.Application.Features.Queries.File
{

    public class GetFileQueryHandler : IRequestHandler<GetFileQuery, GetFileResponse>
    {
        private readonly IMinioService _minioService;
        public GetFileQueryHandler(IMinioService minioService)
        {
            _minioService = minioService;
        }
        public async Task<GetFileResponse> Handle(GetFileQuery request, CancellationToken cancellationToken)
        {
            return await _minioService.GetFileAsync(request.token);
        }

    }
}
