namespace ECafe.Application.DTOs.File
{
    public class FileResponse
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public string Extension { get; set; } = null!;

        public long Size { get; set; }

        public string Url { get; set; } = null!;

        public bool IsAttached { get; set; }
    }
}
