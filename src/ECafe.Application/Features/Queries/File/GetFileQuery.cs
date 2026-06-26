using ECafe.Application.DTOs.File;
using ECafe.Application.Services.MinIO.Abstracts;
using MediatR;

namespace ECafe.Application.Features.Queries.File
{
    public class GetFileQuery : IRequest<GetFileResponse>
    {
        public string token { get; set; } = null!;

    }
}
