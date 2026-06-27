using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public class Wallet : AuditableSoftDeletableEntity<int>
{
    public int UserId { get; set; }

    public decimal Balance { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual ICollection<WalletTransaction> Transactions { get; set; } = [];

    public virtual ICollection<WithdrawRequest> WithdrawRequests { get; set; } = [];
}