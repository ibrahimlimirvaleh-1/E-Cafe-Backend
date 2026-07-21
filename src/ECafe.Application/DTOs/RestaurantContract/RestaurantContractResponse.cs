namespace ECafe.Application.DTOs.RestaurantContract
{
    public class RestaurantContractResponse
    {
        public int Id { get; set; }

        public int RestaurantId { get; set; }

        public string ContractNumber { get; set; } = null!;

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public decimal? CommissionPercent { get; set; }

        public int? StaffSettlementPeriod { get; set; }

        public int PaymentPolicyId { get; set; }

        public int StatusId { get; set; }

        public string? StatusName { get; set; }

        public int? FileId { get; set; }

        public string? FileUrl { get; set; }

        public DateTime? SignedAt { get; set; }

        public int? SignedByUserId { get; set; }

        public string? SignedByUserName { get; set; }
    }
}
