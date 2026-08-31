namespace ECafe.Application.Services.ImageProcessing.Abstract;

public sealed class ImageProcessingResult
{
    public bool IsOptimized { get; init; }

    public byte[]? Bytes { get; init; }

    public string FileName { get; init; } = null!;

    public string ContentType { get; init; } = null!;

    public long Size { get; init; }

    public static ImageProcessingResult Original(string fileName, string contentType, long size)
        => new()
        {
            IsOptimized = false,
            FileName = fileName,
            ContentType = contentType,
            Size = size
        };

    public static ImageProcessingResult Optimized(byte[] bytes, string fileName, string contentType)
        => new()
        {
            IsOptimized = true,
            Bytes = bytes,
            FileName = fileName,
            ContentType = contentType,
            Size = bytes.LongLength
        };
}
