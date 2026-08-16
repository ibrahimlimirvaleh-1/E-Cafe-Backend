namespace ECafe.Application.DTOs.AuditLog
{
    public class AuditLogDetailResponse
    {
        public string Label { get; set; } = null!;

        public string? Value { get; set; }

        public string? OldValue { get; set; }

        public string? NewValue { get; set; }
    }
}
