using ECafe.Application.DTOs.File;
using ECafe.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace ECafe.Application.Services.ImageProcessing.Abstract;

public interface IImageProcessingService
{
    Task<ImageProcessingResult> OptimizeForUploadAsync(
        IFormFile file,
        FileUploadPolicy policy,
        FileTypeCode fileTypeCode,
        CancellationToken cancellationToken = default);
}
