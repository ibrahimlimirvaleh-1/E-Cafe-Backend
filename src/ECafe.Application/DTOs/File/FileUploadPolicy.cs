namespace ECafe.Application.DTOs.File
{
    public sealed class FileUploadPolicy
    {
        public string AllowedExtensions { get; set; } = null!;

        public string AllowedMimeTypes { get; set; } = null!;

        public int MaxSizeMb { get; set; }
    }
}
