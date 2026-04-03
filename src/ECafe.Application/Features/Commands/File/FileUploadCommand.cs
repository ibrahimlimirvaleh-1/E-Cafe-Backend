using MediatR;
using Microsoft.AspNetCore.Http;

namespace ECafe.Application.Features.Commands.File
{
    public class FileUploadCommand : IRequest<string>
    {
        public IFormFile File { get; set; }
    }
}