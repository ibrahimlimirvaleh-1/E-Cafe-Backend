using System.ComponentModel;

namespace ECafe.Domain.Enums
{
    public enum InventoryMovementTypeCode
    {
        [Description("Alış")]
        Purchase = 1,

        [Description("Manual artım")]
        ManualIncrease = 2,

        [Description("Manual azalma")]
        ManualDecrease = 3,

        [Description("Sifariş sərfiyyatı")]
        OrderConsumption = 4,

        [Description("İtki")]
        Waste = 5,

        [Description("Stoka qaytarma")]
        StockReturn = 6,

        [Description("Düzəliş")]
        Correction = 7
    }
}
