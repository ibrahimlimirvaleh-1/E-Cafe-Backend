namespace ECafe.Application.DTOs.Notification
{
    public class CreateNotificationRequest
    {
        public int UserId { get; set; }
        public int? RestaurantId { get; set; }
        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;
        public int TypeId { get; set; }
        public int ChannelId { get; set; }
        public string? PayloadJson { get; set; }
        public string? RelatedEntityType { get; set; }
        public long? RelatedEntityId { get; set; }
    }
}
