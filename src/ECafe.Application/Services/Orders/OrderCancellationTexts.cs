namespace ECafe.Application.Services.Orders;

internal static class OrderCancellationTexts
{
    internal const string Title = "Sifariş ləğv edildi";
    internal const string Section = "order-cancelled";

    /// <summary>Sifarişin ləğvini masa adı ilə bildirir; məbləğ, müştəri və sərbəst səbəb mətnini paylaşmır.</summary>
    internal static string Message(int orderId, string tableName) => $"{tableName} üçün #{orderId} sifarişi ləğv edildi.";
}
