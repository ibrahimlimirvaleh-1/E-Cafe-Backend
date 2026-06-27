using System.ComponentModel;

namespace ECafe.Domain.Enums
{
 
    public enum RoleCode
    {
        [Description("Platforma super administratoru")]
        SuperAdmin = 1,

        [Description("Sahibkar")]
        Owner = 2,

        [Description("Restoran meneceri")]
        Manager = 3,

        [Description("Ofisiant")]
        Waiter = 4,

        [Description("Müştəri")]
        Customer = 5
    }
}