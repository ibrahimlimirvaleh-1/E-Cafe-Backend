using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public partial class RestaurantContract : AuditableSoftDeletableEntity<int>
{
    public int RestaurantId { get; set; }

    public string ContractNumber { get; set; } = null!;

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public decimal? CommissionPercent { get; set; }

    public int? StaffSettlementPeriod { get; set; }

    public int PaymentPolicyId { get; set; }

    public int StatusId { get; set; }

    public int? FileId { get; set; }

    public DateTime? SignedAt { get; set; }

    public int? SignedByUserId { get; set; }

    public virtual Restaurant Restaurant { get; set; } = null!;

    public virtual Status Status { get; set; } = null!;

    public virtual File? File { get; set; }

    public virtual User? SignedByUser { get; set; }
}
