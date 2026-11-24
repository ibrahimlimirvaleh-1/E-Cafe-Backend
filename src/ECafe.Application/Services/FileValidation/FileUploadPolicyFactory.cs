using ECafe.Application.DTOs.File;

namespace ECafe.Application.Services.FileValidation;

public static class FileUploadPolicyFactory
{
    /// <summary>Adi və yaradılmış müqavilə faylları üçün DB-dəki fayl tipinin extension, MIME və ölçü limitlərini dəyişmədən köçürür.</summary>
    public static FileUploadPolicy FromFileType(Domain.Entities.FileType fileType)
        => new()
        {
            AllowedExtensions = fileType.AllowedExtensions,
            AllowedMimeTypes = fileType.AllowedMimeTypes,
            MaxSizeMb = fileType.MaxSizeMb
        };
}
