namespace ECafe.Application.DTOs.AuditLog
{
    public class AuditLogFilterRequest
    {
        public string? Action { get; set; }

        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }

        public int PageNumber { get; set; } = 1;

        public int PageSize { get; set; } = 20;
    }
}
