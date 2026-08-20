using ECafe.Domain.Enums;

namespace ECafe.Application.DTOs.RestaurantContract
{
    public class CreateRestaurantContractRequest
    {
        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public decimal Amount { get; set; }

        public decimal? CommissionPercent { get; set; }

        public int? StaffSettlementPeriod { get; set; }

        public int ExpiryReminderDaysBefore { get; set; } = 1;

        public int PaymentPolicyId { get; set; } = (int)ContractPaymentPolicy.OnlineOnly;
    }
}
