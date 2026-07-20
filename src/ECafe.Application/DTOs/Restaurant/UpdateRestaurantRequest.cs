namespace ECafe.Application.DTOs.Restaurant
{
    public class UpdateRestaurantRequest
    {
        public string Name { get; set; } = null!;

        public string Location { get; set; } = null!;

        public string Phone { get; set; } = null!;

        public string Email { get; set; } = null!;

        public int? RestaurantGroupId { get; set; }

        public string? RestaurantGroupName { get; set; }

        public string? RestaurantGroupLegalName { get; set; }

        public string? BranchName { get; set; }

        public decimal DepositAmount { get; set; }

        public int CancellationWindowMinutes { get; set; } = 60;

        public decimal ServiceFeePercent { get; set; }

        public int StaffSettlementPeriod { get; set; } = 2;

    }
}
