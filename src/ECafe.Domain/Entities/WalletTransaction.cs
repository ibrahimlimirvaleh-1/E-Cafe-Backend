using ECafe.Domain.Entities.Base;
using ECafe.Domain.Enums;

namespace ECafe.Domain.Entities;

public class WalletTransaction : AuditableSoftDeletableEntity<int>
{
    public int WalletId { get; set; }

    public decimal Amount { get; set; }

    public WalletTransactionType Type { get; set; }

    public WalletTransactionSource Source { get; set; }

    public string Description { get; set; } = null!;

    public int? OrderId { get; set; }

    public int? PaymentId { get; set; }

    public int? WithdrawRequestId { get; set; }

    public virtual Wallet Wallet { get; set; } = null!;

    public virtual Order? Order { get; set; }

    public virtual Payment? Payment { get; set; }

    public virtual WithdrawRequest? WithdrawRequest { get; set; }
}