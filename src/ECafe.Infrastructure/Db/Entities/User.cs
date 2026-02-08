using System;
using System.Collections.Generic;

namespace ECafe.Infrastructure.Db.Entities;

public partial class User
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Surname { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public bool IsActive { get; set; }

    public string? ImageUrl { get; set; }

    public decimal? Rating { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public virtual UserRestaurant? UserRestaurant { get; set; }

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();
}
