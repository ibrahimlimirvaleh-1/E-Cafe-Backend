using ECafe.Application.Features.Commands.File;
using ECafe.Application.Features.Queries.File;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{
    public class FileController : BaseController
    {
        [HttpPost("api/v1/file/upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Upload([FromForm] FileUploadCommand command)
        {
            var fileId = await Mediator.Send(command);
            return Ok(fileId);
        }

        [HttpDelete("api/v1/file/{fileId:int}")]
        public async Task<IActionResult> Delete(int fileId)
        {
            await Mediator.Send(new DeleteFileCommand { FileId = fileId });
            return NoContent();
        }

        [HttpGet("api/v1/file/getFile")]
        public async Task<IActionResult> GetFile([FromQuery] GetFileQuery query)
        {
            var file = await Mediator.Send(query);
            return File(file.Bytes, file.ContentType);
        }
    }
}
