namespace ECafe.Application.DTOs.Restaurant.Public
{
    public class PublicRestaurantListItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Location { get; set; } = null!;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? PlaceId { get; set; }
        public string Phone { get; set; } = null!;
        public int? RestaurantGroupId { get; set; }
        public string? RestaurantGroupName { get; set; }
        public string? BranchName { get; set; }
        public decimal? RatingAverage { get; set; }
        public int? RatingCount { get; set; }
        public decimal DepositAmount { get; set; }
        public int CancellationWindowMinutes { get; set; }
        public List<string> ImageUrls { get; set; } = [];
    }

    public class PublicRestaurantProfileDto
    {
        public PublicRestaurantDetailDto Restaurant { get; set; } = null!;
        public List<PublicTableDto> Tables { get; set; } = [];
        public List<PublicMenuCategoryDto> Menu { get; set; } = [];
        public List<PublicStaffDto> Staff { get; set; } = [];
    }

    public class PublicRestaurantDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Location { get; set; } = null!;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? PlaceId { get; set; }
        public string Phone { get; set; } = null!;
        public int? RestaurantGroupId { get; set; }
        public string? RestaurantGroupName { get; set; }
        public string? BranchName { get; set; }
        public decimal? RatingAverage { get; set; }
        public int? RatingCount { get; set; }
        public decimal DepositAmount { get; set; }
        public int CancellationWindowMinutes { get; set; }
        public List<string> ImageUrls { get; set; } = [];
    }

    public class PublicMenuCategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public int SortOrder { get; set; }
        public List<PublicMenuItemDto> Items { get; set; } = [];
    }

    public class PublicMenuItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public decimal BasePrice { get; set; }
        public int SalesCount { get; set; }
        public string? FileUrl { get; set; }
    }

    public class PublicStaffDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Surname { get; set; } = null!;
        public decimal? Rating { get; set; }
        public string Role { get; set; } = null!;
        public decimal? ServiceFeePercent { get; set; }
        public string? FileUrl { get; set; }
    }

    public class PublicTableDto
    {
        public int Id { get; set; }
        public int TableNo { get; set; }
        public string? Name { get; set; }
        public int Capacity { get; set; }
        public bool IsEmpty { get; set; }
    }
}
