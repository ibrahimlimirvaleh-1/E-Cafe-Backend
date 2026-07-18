using System.ComponentModel;

namespace ECafe.Domain.Enums
{
    public enum OrderStatus
    {
        [Description("Sifariş yaradılıb")]
        Created = 1,

        [Description("Sifariş mətbəx tərəfindən qəbul edilib")]
        Accepted,

        [Description("Sifariş hazırlanır")]
        Preparing,

        [Description("Sifariş hazırdır")]
        Ready,

        [Description("Sifariş təqdim olunub")]
        Served,

        [Description("Sifariş online ödənişlə bağlanıb")]
        Closed,

        [Description("Sifariş ləğv edilib")]
        Cancelled
    }
}
