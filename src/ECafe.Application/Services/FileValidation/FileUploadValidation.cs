using ECafe.Application.DTOs.File;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using System.Buffers.Binary;

namespace ECafe.Application.Services.FileValidation;

public static class FileUploadValidation
{
    private const long DefaultMaxUploadSize = 10 * 1024L * 1024L;

    private static readonly Dictionary<string, string[]> KnownUploadTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = [".jpg", ".jpeg"],
        ["image/png"] = [".png"],
        ["image/webp"] = [".webp"],
        ["image/avif"] = [".avif"],
        ["application/pdf"] = [".pdf"],
        ["application/msword"] = [".doc"],
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = [".docx"]
    };

    /// <summary>Dekoder və saxlanma işə düşməzdən əvvəl faylın faktiki başlığını elan olunan MIME ilə tutuşdurur; qısa stream oxunuşlarını da tamamlayır.</summary>
    public static async Task ValidateSignatureAsync(IFormFile file, string contentType, CancellationToken cancellationToken = default)
    {
        var header = new byte[256];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        if (!HasSignature(header.AsSpan(0, read), contentType))
            throw new BusinessRuleException(ErrorCode.FileContentTypeMismatch);
    }

    /// <summary>Yalnız dəstəklənən formatların magic baytlarını qəbul edir; AVIF üçün ftyp qutusundakı uyğun brand-ləri yoxlayır.</summary>
    private static bool HasSignature(ReadOnlySpan<byte> header, string contentType)
        => contentType switch
        {
            "image/jpeg" => header.StartsWith<byte>([0xFF, 0xD8, 0xFF]),
            "image/png" => header.StartsWith<byte>([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
            "image/webp" => header.Length >= 12 && header.StartsWith("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8),
            "image/avif" => HasAvifBrand(header),
            "application/pdf" => header.StartsWith("%PDF"u8),
            "application/msword" => header.StartsWith<byte>([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" =>
                header.StartsWith<byte>([0x50, 0x4B, 0x03, 0x04]) || header.StartsWith<byte>([0x50, 0x4B, 0x05, 0x06]) ||
                header.StartsWith<byte>([0x50, 0x4B, 0x07, 0x08]),
            _ => false
        };

    /// <summary>HEIC və digər ISO media fayllarını AVIF kimi qəbul etmir; əsas və uyğun brand siyahısını ayrılıqda oxuyur.</summary>
    private static bool HasAvifBrand(ReadOnlySpan<byte> header)
    {
        if (header.Length < 16 || !header.Slice(4, 4).SequenceEqual("ftyp"u8)) return false;
        var size = BinaryPrimitives.ReadUInt32BigEndian(header);
        if (size < 16 || size % 4 != 0) return false;
        var end = (int)Math.Min(size, (uint)header.Length);
        for (var offset = 8; offset + 4 <= end; offset += 4)
        {
            if (offset == 12) continue;
            var brand = header.Slice(offset, 4);
            if (brand.SequenceEqual("avif"u8) || brand.SequenceEqual("avis"u8)) return true;
        }
        return false;
    }

    public static string ValidateFileNameAndContentType(
        string fileName,
        string? contentType,
        FileUploadPolicy policy)
    {
        var normalizedContentType = NormalizeContentType(contentType);
        var allowedMimeTypes = SplitAllowedValues(policy.AllowedMimeTypes);

        if (!KnownUploadTypes.TryGetValue(normalizedContentType, out var extensions) ||
            !allowedMimeTypes.Contains(normalizedContentType, StringComparer.OrdinalIgnoreCase))
            throw new BusinessRuleException(ErrorCode.UnsupportedFileType);

        var extension = Path.GetExtension(fileName);
        var allowedExtensions = SplitAllowedValues(policy.AllowedExtensions);
        if (string.IsNullOrWhiteSpace(extension) ||
            !allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) ||
            !extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new BusinessRuleException(ErrorCode.FileExtensionMismatch);

        return normalizedContentType;
    }

    public static void EnsureSizeIsAllowed(long size, FileUploadPolicy policy)
    {
        if (size > GetMaxUploadSizeBytes(policy))
            throw new BusinessRuleException(ErrorCode.FileTooLarge, new { maxSizeMb = policy.MaxSizeMb });
    }

    public static bool AllowsImageOutput(FileUploadPolicy policy, string contentType, string extension)
        => SplitAllowedValues(policy.AllowedMimeTypes).Contains(contentType, StringComparer.OrdinalIgnoreCase) &&
           SplitAllowedValues(policy.AllowedExtensions).Contains(extension, StringComparer.OrdinalIgnoreCase);

    public static bool IsImageContentType(string? contentType)
        => NormalizeContentType(contentType) is "image/jpeg" or "image/png" or "image/webp" or "image/avif";

    /// <summary>MIME parametrini ayırır və hərf ölçüsünü normallaşdırır; boş və yararsız girişləri təhlükəsiz boş tipə çevirir.</summary>
    public static string NormalizeContentType(string? contentType)
        => string.IsNullOrWhiteSpace(contentType)
            ? string.Empty
            : contentType.Split(';', 2)[0].Trim().ToLowerInvariant();

    public static string[] SplitAllowedValues(string values)
        => values
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static long GetMaxUploadSizeBytes(FileUploadPolicy policy)
        => policy.MaxSizeMb > 0
            ? policy.MaxSizeMb * 1024L * 1024L
            : DefaultMaxUploadSize;
}
