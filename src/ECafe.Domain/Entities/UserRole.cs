using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities
{
    public class UserRole : AuditableSoftDeletableEntity<int>
    {
        public int UserId { get; set; }

        public int RoleId { get; set; }

        public virtual Role Role { get; set; } = null!;

        public virtual User User { get; set; } = null!;
    }
}
