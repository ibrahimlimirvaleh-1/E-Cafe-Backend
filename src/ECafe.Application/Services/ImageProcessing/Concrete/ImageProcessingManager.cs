using ECafe.Application.DTOs.File;
using ECafe.Application.Services.FileValidation;
using ECafe.Application.Services.ImageProcessing.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ImageMagick;
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
        "image/webp",
        "image/avif"
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
            AvifQuality = GetInt(section["AvifQuality"], 74),
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

        if (IsAvifOutput())
            return OptimizeToAvif(file, policy, cancellationToken);

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
                GetOutputContentType());
        }
    }

    // Fayl tipi, ölçüsü və siyasətə görə optimizasiyanın lazım olduğunu seçir.
    private bool ShouldOptimize(IFormFile file, FileUploadPolicy policy, FileTypeCode fileTypeCode)
    {
        if (!IsOptimizableFileType(fileTypeCode))
            return false;

        if (!IsSupportedOutputFormat())
            return false;

        var contentType = FileUploadValidation.NormalizeContentType(file.ContentType);
        if (!SupportedInputContentTypes.Contains(contentType))
            return false;

        return FileUploadValidation.AllowsImageOutput(policy, GetOutputContentType(), GetOutputExtension());
    }

    // Yalnız dəstəklənən şəkil tiplərini optimizasiyaya buraxır.
    private bool IsOptimizableFileType(FileTypeCode fileTypeCode)
        => SplitAllowedValues(_options.OptimizedFileTypeCodes)
            .Any(code => Enum.TryParse<FileTypeCode>(code, ignoreCase: true, out var parsed) && parsed == fileTypeCode);

    // Orijinal faylın ölçü və format qaydalarını yoxlayır.
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

    // Emaldan çıxan faylın da yükləmə hədlərini aşmadığını yoxlayır.
    private static void EnsureOptimizedFileIsSafe(byte[] bytes, FileUploadPolicy policy)
    {
        FileUploadValidation.EnsureSizeIsAllowed(bytes.LongLength, policy);
    }

    // Şəkli ölçü limitini aşdıqda nisbəti qoruyaraq kiçildir.
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

    // Seçilən çıxış formatına uyğun encoder yaradır.
    private IImageEncoder BuildEncoder()
        => new WebpEncoder
        {
            Quality = Math.Clamp(_options.WebpQuality, 1, 100)
        };

    // Şəkli AVIF formatında ölçü və keyfiyyət qaydasına uyğun sıxır.
    private ImageProcessingResult OptimizeToAvif(
        IFormFile file,
        FileUploadPolicy policy,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var input = file.OpenReadStream();
            using var image = new MagickImage(input);

            image.AutoOrient();
            image.Strip();
            ResizeIfNeeded(image);

            image.Format = MagickFormat.Avif;
            image.Quality = (uint)Math.Clamp(_options.AvifQuality, 1, 100);

            using var output = new MemoryStream();
            image.Write(output);

            cancellationToken.ThrowIfCancellationRequested();

            var optimizedBytes = output.ToArray();
            EnsureOptimizedFileIsSafe(optimizedBytes, policy);

            return ImageProcessingResult.Optimized(
                optimizedBytes,
                BuildOutputFileName(file.FileName),
                GetOutputContentType());
        }
        catch (MagickException)
        {
            throw new BusinessRuleException(ErrorCode.FileContentTypeMismatch);
        }
    }

    // Şəkli ölçü limitini aşdıqda nisbəti qoruyaraq kiçildir.
    private void ResizeIfNeeded(MagickImage image)
    {
        var maxWidth = Math.Max(1, _options.MaxWidth);
        var maxHeight = Math.Max(1, _options.MaxHeight);

        if (image.Width <= maxWidth && image.Height <= maxHeight)
            return;

        var ratio = Math.Min((double)maxWidth / image.Width, (double)maxHeight / image.Height);
        var width = Math.Max(1u, (uint)Math.Round(image.Width * ratio));
        var height = Math.Max(1u, (uint)Math.Round(image.Height * ratio));

        image.Resize(width, height);
    }

    // Optimallaşdırılmış şəkil üçün yeni uzantılı ad yaradır.
    private string BuildOutputFileName(string fileName)
        => $"{Path.GetFileNameWithoutExtension(fileName)}{GetOutputExtension()}";

    // Konfiqurasiya edilmiş çıxış formatının dəstəkləndiyini yoxlayır.
    private bool IsSupportedOutputFormat()
        => IsWebpOutput() || IsAvifOutput();

    // Çıxış formatının WebP olub-olmadığını müəyyən edir.
    private bool IsWebpOutput()
        => string.Equals(_options.OutputFormat, "webp", StringComparison.OrdinalIgnoreCase);

    // Çıxış formatının AVIF olub-olmadığını müəyyən edir.
    private bool IsAvifOutput()
        => string.Equals(_options.OutputFormat, "avif", StringComparison.OrdinalIgnoreCase);

    // Çıxış şəklinin MIME tipini seçir.
    private string GetOutputContentType()
        => IsAvifOutput() ? "image/avif" : "image/webp";

    // Çıxış şəklinin fayl uzantısını seçir.
    private string GetOutputExtension()
        => IsAvifOutput() ? ".avif" : ".webp";

    // Konfiqurasiyadakı icazəli dəyərləri siyahıya ayırır.
    private static string[] SplitAllowedValues(string values)
        => values
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    // Şəkil emalı ayarını boolean kimi oxuyub ehtiyat dəyər tətbiq edir.
    private static bool GetBool(string? value, bool fallback)
        => bool.TryParse(value, out var parsed) ? parsed : fallback;

    // Şəkil emalı ayarını ədəd kimi oxuyub ehtiyat dəyər tətbiq edir.
    private static int GetInt(string? value, int fallback)
        => int.TryParse(value, out var parsed) ? parsed : fallback;
}
