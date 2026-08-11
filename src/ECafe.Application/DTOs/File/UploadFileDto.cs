using Microsoft.AspNetCore.Http;

namespace ECafe.Application.DTOs.File
{
    public class UploadFileDto
    {
        public IFormFile? File { get; set; }

        public int? FileTypeId { get; set; }

        public UploadFileDto()
        {

        }

        public UploadFileDto(IFormFile? file)
        {
            File = file;
        }
    }
}
