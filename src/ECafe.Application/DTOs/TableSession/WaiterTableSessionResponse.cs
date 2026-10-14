namespace ECafe.Application.DTOs.TableSession;

/// <summary>Ofisiantın öz açıq masa sessiyasının maliyyə və şəxsi əlaqə məlumatları olmayan xidmət görünüşü.</summary>
public sealed class WaiterTableSessionResponse
{
    public int Id { get; init; }
    public int RestaurantId { get; init; }
    public int TableId { get; init; }
    public int TableNo { get; init; }
    public string? TableName { get; init; }
    public int TableCapacity { get; init; }
    public int StatusId { get; init; }
    public int? ReservationId { get; init; }
    public string? CustomerName { get; init; }
    public int? PeopleCount { get; init; }
    public DateTimeOffset OpenedAt { get; init; }
    public DateTimeOffset? MustVacateAt { get; init; }
    public string TimeZone { get; init; } = null!;
}
