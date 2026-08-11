namespace ECafe.Application.DTOs.Auth
{
    public class FileMapData
    {
        public string Token { get; set; } = null!;

        public string FileName { get; set; } = null!;

        public long Size { get; set; }

        public string Url { get; set; } = string.Empty;

        public int FileTypeId { get; set; }
    }
}
