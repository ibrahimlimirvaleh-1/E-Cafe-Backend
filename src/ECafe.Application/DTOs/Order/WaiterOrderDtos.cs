namespace ECafe.Application.DTOs.Order;

public sealed class CreateWaiterOrderRequest
{
    public string? Note { get; init; }
    public List<CreateOrderLineRequest> Items { get; init; } = [];
}

public sealed class CreateOrderLineRequest
{
    public int ItemId { get; init; }
    public int Quantity { get; init; }
    public string? Note { get; init; }
    public decimal? ExpectedUnitPrice { get; init; }
}

public sealed record OrderLineResponse(int Id, int ItemId, string Name, int Quantity,
    decimal UnitPrice, decimal LineTotal, string? Note);

public sealed record SessionOrderResponse(int Id, int RestaurantId, int TableId, int? TableSessionId,
    int? ReservationId, int? WaiterUserId, int SourceId, int StatusId, DateTimeOffset OpenedAt,
    DateTimeOffset? SentToKitchenAt, string? Note, decimal Subtotal, IReadOnlyList<OrderLineResponse> Items,
    DateTimeOffset? ServedAt = null, bool CanMarkServed = false, bool CanCancel = false,
    DateTimeOffset? CancelledAt = null, string? CancellationReason = null);

public sealed record CreateOrderResult(SessionOrderResponse Order, bool IsReplay);
