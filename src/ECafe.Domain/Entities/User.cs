using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public partial class User : AuditableSoftDeletableEntity<int>
{
    public string Name { get; set; } = null!;

    public string Surname { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string Password { get; set; } = null!;

    public bool IsActive { get; set; }

    public int? FileId { get; set; }

    public int RoleId { get; set; }

    public decimal? Rating { get; set; }

    public int SessionVersion { get; set; }

    public virtual File? File { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public virtual UserRestaurant? UserRestaurant { get; set; }

    public virtual Role Role { get; set; } = null!;
    public virtual Wallet? Wallet { get; set; }


    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<UserRefreshToken> RefreshTokens { get; set; } = new List<UserRefreshToken>();

    public virtual ICollection<LoginAttempt> LoginAttempts { get; set; } = new List<LoginAttempt>();

    public virtual ICollection<RestaurantContract> SignedRestaurantContracts { get; set; } = new List<RestaurantContract>();

    public DateTime? PasswordSetAt { get; set; }

    public ICollection<UserPasswordSetupToken> PasswordSetupTokens { get; set; } = new List<UserPasswordSetupToken>();

    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();

}
