using System.ComponentModel;

namespace ECafe.Domain.Enums
{
    public enum InventoryMovementTypeCode
    {
        [Description("Purchase")]
        Purchase = 1,

        [Description("Manual increase")]
        ManualIncrease = 2,

        [Description("Manual decrease")]
        ManualDecrease = 3,

        [Description("Order consumption")]
        OrderConsumption = 4,

        [Description("Waste")]
        Waste = 5,

        [Description("Stock return")]
        StockReturn = 6,

        [Description("Correction")]
        Correction = 7
    }
}
