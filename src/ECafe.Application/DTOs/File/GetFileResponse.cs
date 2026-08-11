namespace ECafe.Application.DTOs.File
{
    public class GetFileResponse
    {
        public GetFileResponse(byte[] bytes, string contentType, string? fileName = null, string? url = null)
        {
            Bytes = bytes;
            ContentType = contentType;
            FileName = fileName;
            Url = url;
        }

        public byte[] Bytes { get; set; }
        public string ContentType { get; set; }
        public string? FileName { get; set; }
        public string? Url { get; set; }
    }
}
