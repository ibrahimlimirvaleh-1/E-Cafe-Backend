using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public class WithdrawRequest : AuditableSoftDeletableEntity<int>
{
    public int WalletId { get; set; }

    public decimal Amount { get; set; }

    public int StatusId { get; set; }

    public int? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? RejectReason { get; set; }

    public virtual Wallet Wallet { get; set; } = null!;

    public virtual Status Status { get; set; } = null!;

    public virtual User? ApprovedByUser { get; set; }
}