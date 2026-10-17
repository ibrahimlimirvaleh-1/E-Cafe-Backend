namespace ECafe.Application.Services.Orders;

internal static class KitchenOrderTexts
{
    internal const string NotificationTitle = "Mətbəxə yeni sifariş göndərildi";
    internal const string Section = "kitchen";
    internal const string ReadySection = "order-ready";
    internal const string ReadyTitle = "Sifariş hazırdır";

    /// <summary>Məsul ofisianta hazır sifarişi və masanı bildirir; müştəri və ödəniş məlumatı paylaşmır.</summary>
    internal static string ReadyMessage(int orderId, string tableName)
        => $"{tableName} üçün #{orderId} sifarişi hazırdır. Mətbəxdən təhvil alın.";

    /// <summary>Mətbəx işçisinə masa və sifariş nömrəsini bildirir; qiymət və müştəri məlumatlarını paylaşmır.</summary>
    internal static string SentMessage(int orderId, string tableName)
        => $"{tableName} üçün #{orderId} sifarişi mətbəx növbəsinə əlavə edildi.";
}
