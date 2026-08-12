using ECafe.Api.Security;
using ECafe.Application.DTOs.File;
using ECafe.Application.Features.Commands.File;
using ECafe.Application.Features.Queries.File;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace ECafe.Api.Controllers
{
    public class FileController : BaseController
    {
        [HasPermission(PermissionCode.ViewRestaurantInfo)]
        [HttpPost("api/v1/file/upload")]
        [Consumes("multipart/form-data")]
        [EnableRateLimiting(RateLimitPolicyNames.FileUpload)]
        public async Task<IActionResult> Upload([FromForm] FileUploadCommand command)
        {
            var file = await Mediator.Send(command);
            return Ok(file);
        }

        [HasPermission(PermissionCode.ViewRestaurantInfo)]
        [HttpDelete("api/v1/file/{fileId:int}")]
        public async Task<IActionResult> Delete(int fileId)
        {
            await Mediator.Send(new DeleteFileCommand { FileId = fileId });
            return NoContent();
        }

        [HasPermission(PermissionCode.ViewRestaurantInfo)]
        [HttpGet("api/v1/file/{fileId:int}")]
        public async Task<IActionResult> GetById(int fileId)
        {
            var file = await Mediator.Send(new GetFileMetadataQuery { FileId = fileId });
            return Ok(file);
        }

        [Authorize]
        [HttpGet("api/v1/files/{fileId:int}/view")]
        [EnableRateLimiting(RateLimitPolicyNames.FileDownload)]
        public async Task<IActionResult> View(int fileId)
        {
            var file = await Mediator.Send(new DownloadFileQuery { FileId = fileId });
            ApplyProtectedFileHeaders(file, "inline");
            return File(file.Bytes, file.ContentType);
        }


        [Authorize]                                                     
        [HttpGet("api/v1/files/{fileId:int}/download")]
        [EnableRateLimiting(RateLimitPolicyNames.FileDownload)]
        public async Task<IActionResult> Download(int fileId)
        {
            var file = await Mediator.Send(new DownloadFileQuery { FileId = fileId });
            ApplyProtectedFileHeaders(file, "attachment");
            return File(file.Bytes, file.ContentType, file.FileName);
        }

        [AllowAnonymous]
        [HttpGet("api/v1/file/getFile")]
        [EnableRateLimiting(RateLimitPolicyNames.FileDownload)]
        public async Task<IActionResult> GetFile([FromQuery] GetFileQuery query)
        {
            var file = await Mediator.Send(query);
            return File(file.Bytes, file.ContentType);
        }

        private void ApplyProtectedFileHeaders(GetFileResponse file, string dispositionType)
        {
            Response.Headers.CacheControl = "no-store, private";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";

            if (string.IsNullOrWhiteSpace(file.FileName))
                return;

            Response.Headers.ContentDisposition = new ContentDispositionHeaderValue(dispositionType)
            {
                FileNameStar = file.FileName
            }.ToString();
        }
    }
}
