namespace ECafe.Application.DTOs.Restaurant
{
    public class RegisterRestaurantRequest
    {
        public string Location { get; set; } = null!;

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public string? PlaceId { get; set; }

        public string Phone { get; set; } = null!;
        public string Email { get; set; } = null!;

        public int? RestaurantGroupId { get; set; }

        public string? RestaurantGroupName { get; set; }

        public string? RestaurantGroupLegalName { get; set; }

        public string? BranchName { get; set; }

        public decimal? RatingAverage { get; set; }

        public int? RatingCount { get; set; }

        public decimal DepositAmount { get; set; }

        public int CancellationWindowMinutes { get; set; } = 60;

        public decimal ServiceFeePercent { get; set; }

        public int StaffSettlementPeriod { get; set; } = 2;

        public RegisterRestaurantOwnerRequest? Owner { get; set; }

        public List<int>? FileIds { get; set; }

    }

    public sealed class RegisterRestaurantOwnerRequest
    {
        public int? Id { get; set; }

        public string? SearchText { get; set; }

        public string? Email { get; set; }

        public string? Phone { get; set; }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }
    }
}
