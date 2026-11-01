namespace ECafe.Application.Services.Realtime;

// Hadisə yalnız sorğunu köhnəldir; status, səbəb, qiymət və şəxsi məlumat daşımır.
public sealed record OrderChangedEvent(Guid EventId, int RestaurantId, int? TableSessionId,
    int? OrderId, DateTime OccurredAtUtc);

