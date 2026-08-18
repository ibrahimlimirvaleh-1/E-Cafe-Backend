namespace ECafe.Application.DTOs.Outbox
{
    public class OutboxMessageFilterRequest
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int? StatusId { get; set; }
        public int? ChannelId { get; set; }
        public string? Search { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
    }
}
