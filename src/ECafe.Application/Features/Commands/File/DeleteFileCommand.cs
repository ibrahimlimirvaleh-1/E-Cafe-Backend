using MediatR;

namespace ECafe.Application.Features.Commands.File
{
    public class DeleteFileCommand : IRequest
    {
        public int FileId { get; set; }
    }
}
