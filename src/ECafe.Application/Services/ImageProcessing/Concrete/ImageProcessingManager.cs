using ECafe.Application.DTOs.File;
using ECafe.Application.Services.FileValidation;
using ECafe.Application.Services.ImageProcessing.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace ECafe.Application.Services.ImageProcessing.Concrete;

public sealed class ImageProcessingManager : IImageProcessingService
{
    private static readonly HashSet<string> SupportedInputContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly ImageProcessingOptions _options;

    public ImageProcessingManager(IConfiguration configuration)
    {
        var section = configuration.GetSection("ImageProcessing");
        _options = new ImageProcessingOptions
        {
            Enabled = GetBool(section["Enabled"], true),
            MaxWidth = GetInt(section["MaxWidth"], 1440),
            MaxHeight = GetInt(section["MaxHeight"], 1440),
            WebpQuality = GetInt(section["WebpQuality"], 82),
            OutputFormat = section["OutputFormat"] ?? "webp",
            OptimizedFileTypeCodes = section["OptimizedFileTypeCodes"] ??
                                     "RestaurantImage,MenuItemImage,UserProfileImage,TemporaryUpload"
        };
    }

    public async Task<ImageProcessingResult> OptimizeForUploadAsync(
        IFormFile file,
        FileUploadPolicy policy,
        FileTypeCode fileTypeCode,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || !ShouldOptimize(file, policy, fileTypeCode))
            return ImageProcessingResult.Original(
                file.FileName,
                FileUploadValidation.NormalizeContentType(file.ContentType),
                file.Length);

        EnsureOriginalFileIsSafe(file, policy);

        await using var input = file.OpenReadStream();
        Image image;
        try
        {
            image = await Image.LoadAsync(input, cancellationToken);
        }
        catch (UnknownImageFormatException)
        {
            throw new BusinessRuleException(ErrorCode.FileContentTypeMismatch);
        }
        catch (InvalidImageContentException)
        {
            throw new BusinessRuleException(ErrorCode.FileContentTypeMismatch);
        }

        using (image)
        {
            image.Metadata.ExifProfile = null;
            image.Metadata.IccProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;

            ResizeIfNeeded(image);

            await using var output = new MemoryStream();
            await image.SaveAsync(output, BuildEncoder(), cancellationToken);

            var optimizedBytes = output.ToArray();
            EnsureOptimizedFileIsSafe(optimizedBytes, policy);

            return ImageProcessingResult.Optimized(
                optimizedBytes,
                BuildOutputFileName(file.FileName),
                "image/webp");
        }
    }

    private bool ShouldOptimize(IFormFile file, FileUploadPolicy policy, FileTypeCode fileTypeCode)
    {
        if (!IsOptimizableFileType(fileTypeCode))
            return false;

        if (!string.Equals(_options.OutputFormat, "webp", StringComparison.OrdinalIgnoreCase))
            return false;

        var contentType = FileUploadValidation.NormalizeContentType(file.ContentType);
        if (!SupportedInputContentTypes.Contains(contentType))
            return false;

        return FileUploadValidation.AllowsWebpOutput(policy);
    }

    private bool IsOptimizableFileType(FileTypeCode fileTypeCode)
        => SplitAllowedValues(_options.OptimizedFileTypeCodes)
            .Any(code => Enum.TryParse<FileTypeCode>(code, ignoreCase: true, out var parsed) && parsed == fileTypeCode);

    private void EnsureOriginalFileIsSafe(IFormFile file, FileUploadPolicy policy)
    {
        FileUploadValidation.EnsureSizeIsAllowed(file.Length, policy);

        var contentType = FileUploadValidation.ValidateFileNameAndContentType(
            file.FileName,
            file.ContentType,
            policy);
        if (!SupportedInputContentTypes.Contains(contentType))
            throw new BusinessRuleException(ErrorCode.UnsupportedFileType);
    }

    private static void EnsureOptimizedFileIsSafe(byte[] bytes, FileUploadPolicy policy)
    {
        FileUploadValidation.EnsureSizeIsAllowed(bytes.LongLength, policy);
    }

    private void ResizeIfNeeded(Image image)
    {
        var maxWidth = Math.Max(1, _options.MaxWidth);
        var maxHeight = Math.Max(1, _options.MaxHeight);

        if (image.Width <= maxWidth && image.Height <= maxHeight)
            return;

        image.Mutate(context => context.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(maxWidth, maxHeight),
            Sampler = KnownResamplers.Lanczos3
        }));
    }

    private IImageEncoder BuildEncoder()
        => new WebpEncoder
        {
            Quality = Math.Clamp(_options.WebpQuality, 1, 100)
        };

    private static string BuildOutputFileName(string fileName)
        => $"{Path.GetFileNameWithoutExtension(fileName)}.webp";

    private static string[] SplitAllowedValues(string values)
        => values
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool GetBool(string? value, bool fallback)
        => bool.TryParse(value, out var parsed) ? parsed : fallback;

    private static int GetInt(string? value, int fallback)
        => int.TryParse(value, out var parsed) ? parsed : fallback;
}
