using ECafe.Application.Features.Commands.File;
using ECafe.Application.Features.Queries.File;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{
    public class FileController : BaseController
    {
        [HttpPost("api/file/upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Upload([FromForm] FileUploadCommand command)
        {
            var fileName = await Mediator.Send(command);
            return Ok(fileName);
        }

        [HttpGet("api/file/getFile")]
        public async Task<IActionResult> GetFile([FromQuery] GetFileQuery query)
        {
            var file = await Mediator.Send(query);
            return File(file.Bytes, file.ContentType);
        }
    }
}
