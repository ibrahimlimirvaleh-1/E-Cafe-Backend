namespace ECafe.Application.DTOs.Order;

public sealed record OrderCancellationRequestResponse(int Id, int StatusId, string Reason, DateTimeOffset RequestedAt,
    DateTimeOffset? ForwardedAt, DateTimeOffset? ResolvedAt, string? DecisionReason);

public sealed record DecideOrderCancellationRequest(bool? Approved, string Reason);
