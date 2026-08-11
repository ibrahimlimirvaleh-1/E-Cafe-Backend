namespace ECafe.Application.DTOs.File
{
    public class UploadGeneratedFileDto
    {
        public byte[] Bytes { get; set; } = [];

        public string FileName { get; set; } = null!;

        public string ContentType { get; set; } = null!;

        public int FileTypeId { get; set; }
    }
}
