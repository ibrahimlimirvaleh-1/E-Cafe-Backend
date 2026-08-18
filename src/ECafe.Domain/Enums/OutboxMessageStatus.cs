using System.ComponentModel;

namespace ECafe.Domain.Enums
{
    public enum OutboxMessageStatus
    {
        [Description("Gözləyir")]
        Pending = 1,

        [Description("İcra olunur")]
        Processing = 2,

        [Description("Göndərildi")]
        Sent = 3,

        [Description("Uğursuz")]
        Failed = 4
    }
}
