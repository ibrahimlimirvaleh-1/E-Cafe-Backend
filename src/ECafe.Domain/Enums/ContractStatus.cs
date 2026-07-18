using System.ComponentModel;

namespace ECafe.Domain.Enums
{
    public enum ContractStatus
    {
        [Description("Layihə")]
        Draft = 1,

        [Description("İmza gözləyir")]
        PendingSignature,

        [Description("Aktiv")]
        Active,

        [Description("Müddəti bitib")]
        Expired,

        [Description("Ləğv edilib")]
        Terminated
    }
}
