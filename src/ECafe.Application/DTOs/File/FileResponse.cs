namespace ECafe.Application.DTOs.File
{
    public class FileResponse
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public string Extension { get; set; } = null!;

        public long Size { get; set; }

        public string Url { get; set; } = null!;

        public string? DownloadUrl { get; set; }

        public int FileTypeId { get; set; }

        public string? FileTypeCode { get; set; }

        public bool IsPublic { get; set; }

        public bool IsAttached { get; set; }
    }
}
