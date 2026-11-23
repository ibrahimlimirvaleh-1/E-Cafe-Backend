namespace ECafe.Domain.Enums;

public enum CustomerAccountClosureBlocker
{
    ActiveStaffProfile,
    ActiveReservation,
    OpenTableSession,
    PendingPayment,
    PendingRefund,
    UnpaidBill,
    WalletBalance,
    UnresolvedOrder
}
