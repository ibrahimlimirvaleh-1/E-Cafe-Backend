namespace ECafe.Application.DTOs.RestaurantContract
{
    public class RestaurantContractDocumentData
    {
        public string ContractNumber { get; set; } = null!;

        public string RestaurantName { get; set; } = null!;

        public string LegalName { get; set; } = null!;

        public string BranchName { get; set; } = null!;

        public string Location { get; set; } = null!;

        public string Phone { get; set; } = null!;

        public string Email { get; set; } = null!;

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public decimal? CommissionPercent { get; set; }

        public int? StaffSettlementPeriod { get; set; }

        public int PaymentPolicyId { get; set; }
    }
}
