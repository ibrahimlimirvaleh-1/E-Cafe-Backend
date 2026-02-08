using System;
using System.Collections.Generic;

namespace ECafe.Infrastructure.Db.Entities;

public partial class StatusType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Status> Statuses { get; set; } = new List<Status>();
}
