namespace ECafe.Application.Services.Realtime;

public sealed record OrderRealtimePayload(int RestaurantId, int? TableSessionId, int? OrderId, bool IncludeKitchen);
