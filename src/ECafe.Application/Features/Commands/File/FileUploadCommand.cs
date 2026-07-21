using ECafe.Application.DTOs.File;
using MediatR;

namespace ECafe.Application.Features.Commands.File
{
    public class FileUploadCommand : UploadFileDto, IRequest<int>
    {
    }
}
