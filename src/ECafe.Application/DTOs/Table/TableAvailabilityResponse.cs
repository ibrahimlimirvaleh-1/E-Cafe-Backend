namespace ECafe.Application.DTOs.Table
{
    public class TableAvailabilityResponse
    {
        public DateTimeOffset ReservedAt { get; set; }

        public bool IsRestaurantOpen { get; set; }

        public bool HasAvailableTable { get; set; }

        public int AvailableCount { get; set; }

        public List<TableResponse> Tables { get; set; } = [];
    }
}
