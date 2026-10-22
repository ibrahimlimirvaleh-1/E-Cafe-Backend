using ECafe.Application.DTOs.Dashboard;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;

namespace ECafe.Application.Repositories.Dashboard;

public sealed record DashboardScope(int UserId, RoleCode Role, int? RestaurantId, bool IsTest);
public sealed record DashboardAccess(IReadOnlyList<DashboardRestaurantOption> Restaurants, bool HasMore, string TimeZone);
public sealed record DashboardTotals(int OrderCount, decimal OrderAmount, int ReservationCount,
    decimal CollectedAmount, decimal DepositCollectedAmount, decimal SessionCollectedAmount, decimal RefundedAmount);
public sealed record DashboardHour(DateTime UtcDate, int UtcHour, decimal Value, int UtcMinute = 0);
public sealed record DashboardLive(int ActiveRestaurants, int ActiveUsers, int ActiveContracts, int TodayReservations,
    int OpenSessions, int KitchenOrders, int ReadyOrders, int DelayedKitchen, int WaitingGuests,
    int LowStock, int ExpiringContracts, int FailedPayments, int PendingRefunds, int PaymentReview, int CloseReview);
public sealed record DashboardPage<T>(IReadOnlyList<T> Rows, int Total);
