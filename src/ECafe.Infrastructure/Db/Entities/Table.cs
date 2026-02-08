using System;
using System.Collections.Generic;

namespace ECafe.Infrastructure.Db.Entities;

public partial class Table
{
    public int Id { get; set; }

    public int RestaurantId { get; set; }

    public int TableNo { get; set; }

    public string? Name { get; set; }

    public int Capacity { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public virtual Restaurant Restaurant { get; set; } = null!;
}
