using ECafe.Application.DTOs.File;
using ECafe.Domain.Exceptions;

namespace ECafe.Application.Services.FileValidation;

public static class FileUploadValidation
{
    private const long DefaultMaxUploadSize = 10 * 1024L * 1024L;

    private static readonly Dictionary<string, string[]> KnownUploadTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = [".jpg", ".jpeg"],
        ["image/png"] = [".png"],
        ["image/webp"] = [".webp"],
        ["application/pdf"] = [".pdf"],
        ["application/msword"] = [".doc"],
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = [".docx"]
    };

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

    public static bool AllowsWebpOutput(FileUploadPolicy policy)
        => SplitAllowedValues(policy.AllowedMimeTypes).Contains("image/webp", StringComparer.OrdinalIgnoreCase) &&
           SplitAllowedValues(policy.AllowedExtensions).Contains(".webp", StringComparer.OrdinalIgnoreCase);

    public static bool IsImageContentType(string? contentType)
        => NormalizeContentType(contentType) is "image/jpeg" or "image/png" or "image/webp";

    public static string NormalizeContentType(string? contentType)
        => string.IsNullOrWhiteSpace(contentType)
            ? string.Empty
            : contentType.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)[0];

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
