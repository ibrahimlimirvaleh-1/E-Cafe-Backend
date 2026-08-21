namespace ECafe.Application.DTOs.Table
{
    public class CopyTableRequest
    {
        public int? TableNo { get; set; }

        public string? Name { get; set; }

        public int CopyCount { get; set; } = 1;

        public List<CopyTableItemRequest> Copies { get; set; } = new();
    }

    public class CopyTableItemRequest
    {
        public int? TableNo { get; set; }

        public string? Name { get; set; }
    }
}
