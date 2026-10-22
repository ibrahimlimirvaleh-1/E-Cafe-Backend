using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Enums;

namespace ECafe.Application.Mappings;

public static class DashboardAlertMapper
{
    /// <summary>Yalnız sıfırdan böyük və rolun görə bildiyi əməliyyat xəbərdarlıqlarını qaytarır; maliyyə detalları menecerə ötürülmür.</summary>
    public static IReadOnlyList<DashboardAlert> Map(RoleCode role, DashboardLive live, int? restaurantId)
    {
        var alerts = new List<DashboardAlert>();
        if (role != RoleCode.SuperAdmin)
        {
            AddAlert(alerts, "readyOrders", live.ReadyOrders, "warning", restaurantId);
            AddAlert(alerts, "delayedKitchen", live.DelayedKitchen, "danger", restaurantId);
            AddAlert(alerts, "waitingGuests", live.WaitingGuests, "warning", restaurantId);
            AddAlert(alerts, "lowStock", live.LowStock, "warning", restaurantId);
            AddAlert(alerts, "closeReview", live.CloseReview, "warning", restaurantId);
        }
        if (role != RoleCode.Manager)
        {
            AddAlert(alerts, "expiringContracts", live.ExpiringContracts, "warning", restaurantId);
            AddAlert(alerts, "failedPayments", live.FailedPayments, "danger", restaurantId);
            AddAlert(alerts, "pendingRefunds", live.PendingRefunds, "warning", restaurantId);
            AddAlert(alerts, "paymentReview", live.PaymentReview, "danger", restaurantId);
        }
        return alerts;
    }

    /// <summary>Boş xəbərdarlığı UI-yə göndərmir; say və restoran kontekstini dəyişmədən saxlayır.</summary>
    private static void AddAlert(List<DashboardAlert> alerts, string key, int count, string tone, int? restaurantId)
    {
        if (count > 0) alerts.Add(new(key, count, tone, restaurantId));
    }
}
