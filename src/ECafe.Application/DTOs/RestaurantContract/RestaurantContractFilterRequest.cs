namespace ECafe.Application.DTOs.RestaurantContract
{
    public class RestaurantContractFilterRequest
    {
        public int PageNumber { get; set; }

        public int PageSize { get; set; }

        public int? StatusId { get; set; }

        public string? Search { get; set; }

        public DateTime? EndDateFrom { get; set; }

        public DateTime? EndDateTo { get; set; }

        public int? ExpiringInDays { get; set; }
    }
}
