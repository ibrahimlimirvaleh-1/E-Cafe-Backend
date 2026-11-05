using ECafe.Domain.Enums;

namespace ECafe.Application.Services.Orders;

internal static class KitchenOrderTexts
{
    internal const string NotificationTitle = "Mətbəxə yeni sifariş göndərildi";
    internal const string Section = "kitchen";
    internal const string ReadySection = "order-ready";
    internal const string ReadyTitle = "Sifariş hazırdır";
    internal const string ProgressSection = "order-progress";

    /// <summary>Ofisianta mərhələni masa və sifariş nömrəsi ilə bildirir; hazır bildirişinin mövcud mətnini saxlayır.</summary>
    internal static (NotificationType Type, string Title, string Message) WaiterProgress(OrderStatus status, int orderId, string tableName)
        => status switch
        {
            OrderStatus.Accepted => (NotificationType.WaiterOrderAccepted,
                "Mətbəx sifarişi qəbul etdi", $"Mətbəx {tableName} üçün #{orderId} nömrəli sifarişi qəbul etdi."),
            OrderStatus.Preparing => (NotificationType.WaiterOrderPreparing,
                "Mətbəx hazırlamağa başladı", $"Mətbəx {tableName} üçün #{orderId} nömrəli sifarişi hazırlamağa başladı."),
            OrderStatus.Ready => (NotificationType.OrderReady, ReadyTitle, ReadyMessage(orderId, tableName)),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

    /// <summary>Mətbəxin hər keçidi üçün müştəriyə uyğun, şəxsi məlumat daşımayan bildiriş mətnini seçir.</summary>
    internal static (NotificationType Type, string Title, string Message) CustomerProgress(OrderStatus status, int orderId)
        => status switch
        {
            OrderStatus.Accepted => (NotificationType.CustomerOrderAccepted,
                "Sifarişinizi mətbəx qəbul etdi", $"Mətbəx #{orderId} nömrəli sifarişinizi qəbul etdi."),
            OrderStatus.Preparing => (NotificationType.CustomerOrderPreparing,
                "Sifarişiniz hazırlanır", $"#{orderId} nömrəli sifarişiniz hazırlanmağa başladı."),
            OrderStatus.Ready => (NotificationType.CustomerOrderReady,
                "Sifarişiniz hazırdır", $"#{orderId} nömrəli sifarişiniz hazırdır və təqdim olunmağı gözləyir."),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

    /// <summary>Məsul ofisianta hazır sifarişi və masanı bildirir; müştəri və ödəniş məlumatı paylaşmır.</summary>
    internal static string ReadyMessage(int orderId, string tableName)
        => $"{tableName} üçün #{orderId} sifarişi hazırdır. Mətbəxdən təhvil alın.";

    /// <summary>Mətbəx işçisinə masa və sifariş nömrəsini bildirir; qiymət və müştəri məlumatlarını paylaşmır.</summary>
    internal static string SentMessage(int orderId, string tableName)
        => $"{tableName} üçün #{orderId} sifarişi mətbəx növbəsinə əlavə edildi.";
}
