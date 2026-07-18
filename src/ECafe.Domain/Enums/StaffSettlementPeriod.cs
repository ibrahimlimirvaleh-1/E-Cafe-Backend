using System.ComponentModel;

namespace ECafe.Domain.Enums
{
    public enum StaffSettlementPeriod
    {
        [Description("Günlük")]
        Daily = 1,

        [Description("Həftəlik")]
        Weekly = 2,

        [Description("İki həftəlik")]
        Biweekly = 3,

        [Description("Aylıq")]
        Monthly = 4,

        [Description("Manual")]
        Manual = 5
    }
}
