using System.ComponentModel;

namespace ECafe.Domain.Enums
{
    public enum RoleCode
    {
        [Description("Sistem administratoru")]
        Admin = 1,

        [Description("Restoran meneceri")]
        Manager,

        [Description("Ofisiant")]
        Waiter,

        [Description("Müştəri")]
        Customer
    }

}
