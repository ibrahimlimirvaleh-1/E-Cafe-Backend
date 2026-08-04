using System.ComponentModel;

namespace ECafe.Domain.Enums;

public enum OrderSource
{
    [Description("Müştəri")]
    Customer = 1,

    [Description("Ofisiant")]
    Waiter,

    [Description("Sistem")]
    System,

    [Description("Admin")]
    Admin
}
