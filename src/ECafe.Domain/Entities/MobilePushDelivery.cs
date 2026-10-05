using ECafe.Domain.Enums;

namespace ECafe.Domain.Entities;

public sealed class MobilePushDelivery
{
    public Guid Id { get; set; }
    public Guid OutboxEventId { get; set; }
    public Guid InstallationId { get; set; }
    public int UserId { get; set; }
    public int RestaurantId { get; set; }
    public MobilePushDeliveryStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? FirstSentAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? TicketId { get; set; }
    public string? SentTokenHash { get; set; }
    public string? LastErrorCode { get; set; }
}
