namespace ECafe.Application.DTOs.Order;

public sealed record OrderCancellationRequestResponse(int Id, int StatusId, string Reason, DateTimeOffset RequestedAt,
    DateTimeOffset? ForwardedAt, DateTimeOffset? ResolvedAt, string? DecisionReason,
    bool? IngredientsUnused = null, DateTimeOffset? StockReturnedAt = null);

public sealed record DecideOrderCancellationRequest(bool? Approved, string Reason, bool? IngredientsUnused = null);
