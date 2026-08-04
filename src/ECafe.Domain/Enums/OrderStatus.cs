using System.ComponentModel;

namespace ECafe.Domain.Enums
{
    public enum OrderStatus
    {
        [Description("Sifariş yaradılıb")]
        Created = 1,

        [Description("Sifariş mətbəx tərəfindən qəbul edilib")]
        Accepted = 2,

        [Description("Sifariş hazırlanır")]
        Preparing = 3,

        [Description("Sifariş hazırdır")]
        Ready = 4,

        [Description("Sifariş təqdim olunub")]
        Served = 5,

        [Description("Sifariş bağlanıb")]
        Closed = 6,

        [Description("Sifariş ləğv edilib")]
        Cancelled = 7,

        [Description("Sifariş planlaşdırılıb")]
        Scheduled = 8
    }
}
