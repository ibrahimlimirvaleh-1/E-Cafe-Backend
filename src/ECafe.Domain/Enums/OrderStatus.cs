using System.ComponentModel;

namespace ECafe.Domain.Enums
{
    public enum OrderStatus
    {
        [Description("Sifariş açılıb")]
        Open=1,

        [Description("Sifariş hazırlanır")]
        InProgress,

        [Description("Sifariş təqdim olunub")]
        Served,

        [Description("Sifariş bağlanıb")]
        Closed,

        [Description("Sifariş ləğv edilib")]
        Cancelled
    }

}
