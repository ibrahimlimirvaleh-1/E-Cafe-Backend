using ECafe.Application.DTOs.Order;

namespace ECafe.Application.DTOs.TableSession;

/// <summary>Ofisiantın öz açıq masalarındakı təqdim edilə bilən sifarişi masa konteksti ilə göstərir.</summary>
public sealed record WaiterReadyOrderResponse(SessionOrderResponse Order, string? TableName,
    int TableNo, string TimeZone, DateTimeOffset ReadyAt);
