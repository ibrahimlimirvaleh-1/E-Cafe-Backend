using System.ComponentModel;

namespace ECafe.Domain.Enums
{
    public enum OutboxMessageChannel
    {
        [Description("Email")]
        Email = 1,

        [Description("App bildirişi")]
        InApp = 2
    }
}
