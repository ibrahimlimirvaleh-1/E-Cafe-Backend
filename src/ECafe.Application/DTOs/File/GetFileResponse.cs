namespace ECafe.Application.DTOs.File
{
    public class GetFileResponse
    {
        public GetFileResponse(byte[] bytes, string contentType, string? url = null)
        {
            Bytes = bytes;
            ContentType = contentType;
            Url = url;
        }

        public byte[] Bytes { get; set; }
        public string ContentType { get; set; }
        public string Url { get; set; }
    }
}
