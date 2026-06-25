using System.ComponentModel;

namespace ECafe.Domain.Enums
{
    public enum ItemStatus
    {
        [Description("Mövcuddur")]
        Available = 1,

        [Description("Məhduddur")]
        Limited = 2,

        [Description("Bitib")]
        OutOfStock = 3
    }
}
