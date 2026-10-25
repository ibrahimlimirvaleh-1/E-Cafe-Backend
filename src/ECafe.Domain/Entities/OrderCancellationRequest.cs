using ECafe.Domain.Enums;

namespace ECafe.Domain.Entities;

public sealed class OrderCancellationRequest
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public OrderCancellationRequestStatus StatusId { get; set; }
    public string Reason { get; set; } = "";
    public DateTime RequestedAt { get; set; }
    public int RequestedByUserId { get; set; }
    public DateTime? ForwardedAt { get; set; }
    public int? ForwardedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public int? ResolvedByUserId { get; set; }
    public string? DecisionReason { get; set; }
    public Order Order { get; set; } = null!;
}
