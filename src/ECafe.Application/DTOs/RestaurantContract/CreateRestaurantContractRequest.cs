using ECafe.Domain.Enums;

namespace ECafe.Application.DTOs.RestaurantContract
{
    public class CreateRestaurantContractRequest
    {
        public string ContractNumber { get; set; } = null!;

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public decimal? CommissionPercent { get; set; }

        public int? StaffSettlementPeriod { get; set; }

        public int PaymentPolicyId { get; set; } = (int)ContractPaymentPolicy.OnlineOnly;

        public int? FileId { get; set; }
    }
}
