namespace ECafe.Application.DTOs.Restaurant
{
    public class GetAllRestaurantsResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;

        public string Location { get; set; } = null!;

        public string Phone { get; set; } = null!;

        public int? RestaurantGroupId { get; set; }

        public string? RestaurantGroupName { get; set; }

        public string? BranchName { get; set; }

        public decimal? RatingAverage { get; set; }

        public int? RatingCount { get; set; }

        public decimal DepositAmount { get; set; }

        public int CancellationWindowMinutes { get; set; }

        public decimal ServiceFeePercent { get; set; }

        public List<string>? ImageUrls { get; set; }
    }
}
