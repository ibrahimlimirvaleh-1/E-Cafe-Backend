namespace ECafe.Application.Services.Orders;

internal static class KitchenOrderTexts
{
    internal const string NotificationTitle = "Mətbəxə yeni sifariş göndərildi";
    internal const string Section = "kitchen";

    /// <summary>Ad verilməmiş masanı nömrəsi ilə göstərərək mətbəxdə boş masa başlığının qarşısını alır.</summary>
    internal static string TableName(ECafe.Domain.Entities.Table table)
        => string.IsNullOrWhiteSpace(table.Name) ? $"Masa-{table.TableNo}" : table.Name;

    /// <summary>Mətbəx işçisinə masa və sifariş nömrəsini bildirir; qiymət və müştəri məlumatlarını paylaşmır.</summary>
    internal static string SentMessage(int orderId, string tableName)
        => $"{tableName} üçün #{orderId} sifarişi mətbəx növbəsinə əlavə edildi.";
}
