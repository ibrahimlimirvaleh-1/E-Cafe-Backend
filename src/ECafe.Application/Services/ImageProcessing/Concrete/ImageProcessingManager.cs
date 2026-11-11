using ECafe.Application.DTOs.File;
using ECafe.Application.Services.FileValidation;
using ECafe.Application.Services.ImageProcessing.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ImageMagick;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.ImageProcessing.Concrete;

public sealed class ImageProcessingManager : IImageProcessingService, IDisposable
{
    private static readonly HashSet<string> SupportedInputContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/avif"
    };

    private readonly ImageProcessingOptions _options;
    private readonly SemaphoreSlim _operations;

    public ImageProcessingManager(IConfiguration configuration)
    {
        var section = configuration.GetSection("ImageProcessing");
        _options = new ImageProcessingOptions
        {
            Enabled = GetBool(section["Enabled"], true),
            MaxWidth = GetInt(section["MaxWidth"], 1440),
            MaxHeight = GetInt(section["MaxHeight"], 1440),
            MaxInputDimension = Math.Clamp(GetInt(section["MaxInputDimension"], 12000), 1, 32768),
            MaxInputPixels = Math.Clamp(GetInt(section["MaxInputPixels"], 24_000_000), 1, 64_000_000),
            MaxInputFrames = Math.Clamp(GetInt(section["MaxInputFrames"], 16), 1, 64),
            MaxConcurrentOperations = Math.Clamp(GetInt(section["MaxConcurrentOperations"], 2), 1, 4),
            QueueTimeoutSeconds = Math.Clamp(GetInt(section["QueueTimeoutSeconds"], 5), 1, 30),
            WebpQuality = GetInt(section["WebpQuality"], 82),
            AvifQuality = GetInt(section["AvifQuality"], 74),
            OutputFormat = section["OutputFormat"] ?? "webp",
            OptimizedFileTypeCodes = section["OptimizedFileTypeCodes"] ??
                                     "RestaurantImage,MenuItemImage,UserProfileImage,TemporaryUpload"
        };
        _operations = new SemaphoreSlim(_options.MaxConcurrentOperations, _options.MaxConcurrentOperations);
    }

    /// <summary>Ortaq emal limitində faktiki formatı və başlıq ölçülərini yoxlayır; yalnız seçilmiş dekoderi işlədir, mövcud çıxış siyasətini saxlayır.</summary>
    public async Task<ImageProcessingResult> OptimizeForUploadAsync(IFormFile file, FileUploadPolicy policy,
        FileTypeCode fileTypeCode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_options.Enabled || !ShouldOptimize(file, policy, fileTypeCode))
            return ImageProcessingResult.Original(file.FileName,
                FileUploadValidation.NormalizeContentType(file.ContentType), file.Length);

        if (!await _operations.WaitAsync(TimeSpan.FromSeconds(_options.QueueTimeoutSeconds), cancellationToken))
            throw new BusinessRuleException(ErrorCode.ImageProcessingBusy);
        try
        {
            FileUploadValidation.EnsureSizeIsAllowed(file.Length, policy);
            var contentType = FileUploadValidation.ValidateFileNameAndContentType(file.FileName, file.ContentType, policy);
            await FileUploadValidation.ValidateSignatureAsync(file, contentType, cancellationToken);
            return await OptimizeImageAsync(file, policy, contentType, cancellationToken);
        }
        catch (MagickException)
        {
            throw new BusinessRuleException(ErrorCode.FileContentTypeMismatch);
        }
        finally
        {
            _operations.Release();
        }
    }

    /// <summary>Şəkli rasterə çevirmədən məhdud ping edir; AVIF çıxışında əvvəlki ilk kadr, WebP-də icazəli animasiya saxlanılır.</summary>
    private async Task<ImageProcessingResult> OptimizeImageAsync(IFormFile file, FileUploadPolicy policy,
        string contentType, CancellationToken cancellationToken)
    {
        var format = InputFormat(contentType);
        var settings = new MagickReadSettings { Format = format, FrameIndex = 0, FrameCount = (uint)_options.MaxInputFrames + 1 };
        using (var headers = new MagickImageCollection())
        {
            await using var header = file.OpenReadStream();
            await headers.PingAsync(header, settings, cancellationToken);
            EnsureImagesAreSafe(headers);
        }

        // Sərbəst format aşkarlanması əvəzinə təsdiqlənmiş MIME-yə uyğun native oxucu seçilir.
        if (IsAvifOutput()) settings.FrameCount = 1;
        await using var input = file.OpenReadStream();
        using var images = new MagickImageCollection();
        await images.ReadAsync(input, settings, cancellationToken);
        EnsureImagesAreSafe(images);
        var outputFormat = IsAvifOutput() ? MagickFormat.Avif : MagickFormat.WebP;
        foreach (var image in images)
        {
            cancellationToken.ThrowIfCancellationRequested();
            image.AutoOrient();
            image.Strip();
            ResizeIfNeeded(image);
            image.Format = outputFormat;
            image.Quality = (uint)Math.Clamp(IsAvifOutput() ? _options.AvifQuality : _options.WebpQuality, 1, 100);
        }
        await using var output = new MemoryStream();
        await images.WriteAsync(output, outputFormat, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = output.ToArray();
        FileUploadValidation.EnsureSizeIsAllowed(bytes.LongLength, policy);
        return ImageProcessingResult.Optimized(bytes, BuildOutputFileName(file.FileName), GetOutputContentType());
    }

    /// <summary>Hər kadrın ölçüsünü və bütün kadrların ümumi piksel büdcəsini daşma yaratmadan yoxlayır.</summary>
    private void EnsureImagesAreSafe(MagickImageCollection images)
    {
        if (images.Count == 0 || images.Count > _options.MaxInputFrames)
            throw new BusinessRuleException(ErrorCode.ImageLimitsExceeded);
        long totalPixels = 0;
        foreach (var image in images)
        {
            if (image.Width == 0 || image.Height == 0 || image.Width > _options.MaxInputDimension || image.Height > _options.MaxInputDimension)
                throw new BusinessRuleException(ErrorCode.ImageLimitsExceeded);
            totalPixels += (long)image.Width * image.Height;
        }
        if (totalPixels > _options.MaxInputPixels) throw new BusinessRuleException(ErrorCode.ImageLimitsExceeded);
    }

    /// <summary>Yalnız JPEG/PNG/WebP/AVIF oxucusunu seçir; TIFF və digər native dekoderlərə yönəlməni rədd edir.</summary>
    private static MagickFormat InputFormat(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/jpeg" => MagickFormat.Jpeg,
        "image/png" => MagickFormat.Png,
        "image/webp" => MagickFormat.WebP,
        "image/avif" => MagickFormat.Avif,
        _ => throw new BusinessRuleException(ErrorCode.UnsupportedFileType)
    };

    /// <summary>Mövcud optimizasiya seçimini saxlayır; sənəd və söndürülmüş rejim saxlanma yoxlamalarına keçir.</summary>
    private bool ShouldOptimize(IFormFile file, FileUploadPolicy policy, FileTypeCode fileTypeCode)
        => SplitAllowedValues(_options.OptimizedFileTypeCodes).Any(code =>
               Enum.TryParse<FileTypeCode>(code, true, out var parsed) && parsed == fileTypeCode) &&
           (_options.OutputFormat.Equals("webp", StringComparison.OrdinalIgnoreCase) || IsAvifOutput()) &&
           SupportedInputContentTypes.Contains(FileUploadValidation.NormalizeContentType(file.ContentType)) &&
           FileUploadValidation.AllowsImageOutput(policy, GetOutputContentType(), GetOutputExtension());

    /// <summary>Təsdiqlənmiş rasteri əvvəlki aspekt nisbəti ilə çıxış ölçülərinə kiçildir.</summary>
    private void ResizeIfNeeded(IMagickImage<byte> image)
    {
        var maxWidth = Math.Max(1, _options.MaxWidth);
        var maxHeight = Math.Max(1, _options.MaxHeight);
        if (image.Width <= maxWidth && image.Height <= maxHeight) return;
        var ratio = Math.Min((double)maxWidth / image.Width, (double)maxHeight / image.Height);
        image.Resize(Math.Max(1u, (uint)Math.Round(image.Width * ratio)), Math.Max(1u, (uint)Math.Round(image.Height * ratio)));
    }

    /// <summary>Orijinal fayl adının gövdəsini saxlayaraq çıxış uzantısını seçir.</summary>
    private string BuildOutputFileName(string fileName) => $"{Path.GetFileNameWithoutExtension(fileName)}{GetOutputExtension()}";

    /// <summary>AVIF çıxış rejimini hərf ölçüsündən asılı olmadan tanıyır.</summary>
    private bool IsAvifOutput() => _options.OutputFormat.Equals("avif", StringComparison.OrdinalIgnoreCase);

    /// <summary>Seçilmiş çıxış formatına uyğun MIME qaytarır.</summary>
    private string GetOutputContentType() => IsAvifOutput() ? "image/avif" : "image/webp";

    /// <summary>Seçilmiş çıxış formatına uyğun uzantı qaytarır.</summary>
    private string GetOutputExtension() => IsAvifOutput() ? ".avif" : ".webp";

    /// <summary>Vergüllə ayrılmış fayl məqsədlərini normallaşdırır və təkrarları silir.</summary>
    private static string[] SplitAllowedValues(string values)
        => values.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>Səhv boolean ayarında mövcud standartı saxlayır.</summary>
    private static bool GetBool(string? value, bool fallback) => bool.TryParse(value, out var parsed) ? parsed : fallback;

    /// <summary>Səhv rəqəm ayarında təhlükəsiz standartı saxlayır.</summary>
    private static int GetInt(string? value, int fallback) => int.TryParse(value, out var parsed) ? parsed : fallback;

    /// <summary>Tətbiq dayandırıldıqda singleton emal semaforunun resursunu buraxır.</summary>
    public void Dispose() => _operations.Dispose();
}
