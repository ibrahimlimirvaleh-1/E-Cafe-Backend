namespace ECafe.Application.DTOs.Notification
{
    public class NotificationResponse
    {
        public int Id { get; set; }
        public int? RestaurantId { get; set; }
        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;
        public int TypeId { get; set; }
        public string TypeName { get; set; } = null!;
        public int ChannelId { get; set; }
        public int StatusId { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public string? PayloadJson { get; set; }
        public string? RelatedEntityType { get; set; }
        public long? RelatedEntityId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
