using ECafe.Application.DTOs.File;
using MediatR;

namespace ECafe.Application.Features.Queries.File
{
    public class DownloadFileQuery : IRequest<GetFileResponse>
    {
        public int FileId { get; set; }
    }
}
