using System.ComponentModel;

namespace ECafe.Domain.Enums
{
    public enum UnitCode
    {
        [Description("Kiloqram")]
        Kilogram = 1,

        [Description("Qram")]
        Gram = 2,

        [Description("Litr")]
        Liter = 3,

        [Description("Millilitr")]
        Milliliter = 4,

        [Description("Ədəd")]
        Piece = 5
    }
}
