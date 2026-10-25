using ECafe.Domain.Enums;

namespace ECafe.Application.Services.Orders;

internal static class OrderCancellationRequestTexts
{
    public const string Title = "Sifarişin ləğv sorğusu";
    public const string Section = "order-cancellation-request";
    public const string ExpiredReason = "Sifariş artıq hazırdır; ləğv sorğusu mətbəxə vaxtında ötürülməyib.";

    /// <summary>Məxfi sərbəst səbəb əvəzinə yalnız sorğu mərhələsini bildirən lokallaşdırılan mətn yaradır.</summary>
    public static string Message(int orderId, OrderCancellationRequestStatus state) => state switch
    {
        OrderCancellationRequestStatus.AwaitingWaiter => $"#{orderId} sifarişi üçün müştəri ləğv sorğusu göndərdi.",
        OrderCancellationRequestStatus.PendingKitchen => $"#{orderId} sifarişinin ləğvi mətbəx qərarını gözləyir.",
        OrderCancellationRequestStatus.Approved => $"Mətbəx #{orderId} sifarişinin ləğvini təsdiqlədi.",
        OrderCancellationRequestStatus.Rejected => $"Mətbəx #{orderId} sifarişinin ləğvini rədd etdi.",
        _ => $"#{orderId} sifarişinin ləğv sorğusu artıq aktual deyil."
    };
}
