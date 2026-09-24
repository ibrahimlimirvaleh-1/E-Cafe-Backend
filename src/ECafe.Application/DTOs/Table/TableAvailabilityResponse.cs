namespace ECafe.Application.DTOs.Table
{
    public class TableAvailabilityResponse
    {
        public DateTimeOffset ReservedAt { get; set; }

        public int ReservationPreBlockMinutes { get; set; }

        public int TableTurnoverBufferMinutes { get; set; }

        public string? RestaurantTimeZone { get; set; }

        public string MessageCode { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public bool IsRestaurantOpen { get; set; }

        public bool HasAvailableTable { get; set; }

        public int AvailableCount { get; set; }

        public List<TableResponse> Tables { get; set; } = [];
    }
}
