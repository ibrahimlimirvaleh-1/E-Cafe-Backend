namespace ECafe.Application.DTOs.Order;

public sealed record CustomerTableSessionResponse(int Id, int RestaurantId, string RestaurantName,
    int TableId, string TableName, int? ReservationId, int StatusId, DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt, string TimeZone);

public sealed record CustomerOrderLineResponse(int Id, int ItemId, string Name, int Quantity,
    decimal UnitPrice, decimal LineTotal);

public sealed record CustomerSessionOrderResponse(int Id, int RestaurantId, int TableId, int TableSessionId,
    int? ReservationId, int StatusId, DateTimeOffset OpenedAt, DateTimeOffset? SentToKitchenAt,
    DateTimeOffset? AcceptedAt, DateTimeOffset? PreparingAt, DateTimeOffset? ReadyAt,
    DateTimeOffset? ServedAt, DateTimeOffset? ClosedAt, decimal Subtotal,
    IReadOnlyList<CustomerOrderLineResponse> Items);
