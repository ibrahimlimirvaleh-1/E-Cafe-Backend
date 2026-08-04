using System.ComponentModel;

namespace ECafe.Domain.Enums;

public enum TableSessionStatus
{
    [Description("Masa sessiyası açıqdır")]
    Open = 1,

    [Description("Masa sessiyası bağlanıb")]
    Closed,

    [Description("Masa sessiyası ləğv edilib")]
    Cancelled
}
