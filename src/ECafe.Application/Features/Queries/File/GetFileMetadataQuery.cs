using ECafe.Application.DTOs.File;
using MediatR;

namespace ECafe.Application.Features.Queries.File
{
    public class GetFileMetadataQuery : IRequest<FileResponse>
    {
        public int FileId { get; set; }
    }
}
