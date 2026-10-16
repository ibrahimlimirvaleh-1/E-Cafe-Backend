namespace ECafe.Application.DTOs.Order;

public sealed record KitchenOrderLineResponse(int Id, int ItemId, string Name, int Quantity, string? Note);

public sealed record KitchenOrderResponse(int Id, int RestaurantId, int TableId, string TableName,
    int? TableSessionId, int? WaiterUserId, int StatusId, DateTimeOffset OpenedAt,
    DateTimeOffset SentToKitchenAt, string? Note, IReadOnlyList<KitchenOrderLineResponse> Items, string TimeZone,
    DateTimeOffset? AcceptedAt, DateTimeOffset? PreparingAt, DateTimeOffset? ReadyAt,
    int? AcceptedByUserId, int? PreparingByUserId, int? ReadyByUserId,
    bool CanAccept, bool CanStartPreparing, bool CanMarkReady);
