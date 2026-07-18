using System.Linq.Expressions;
using ECafe.Domain.Entities;
using ECafe.Domain.Entities.Base;
using ECafe.Infrastructure.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ECafe.Infrastructure.Context;

public partial class ECafeDbContext : DbContext
{
    public ECafeDbContext(DbContextOptions<ECafeDbContext> options) : base(options) { }

    public virtual DbSet<Category> Categories { get; set; } = null!;
    public virtual DbSet<Domain.Entities.File> Files { get; set; } = null!;
    public virtual DbSet<Order> Orders { get; set; } = null!;
    public virtual DbSet<OrderItem> OrderItems { get; set; } = null!;
    public virtual DbSet<Payment> Payments { get; set; } = null!;
    public virtual DbSet<Permission> Permissions { get; set; } = null!;
    public virtual DbSet<Reservation> Reservations { get; set; } = null!;
    public virtual DbSet<Restaurant> Restaurants { get; set; } = null!;
    public virtual DbSet<Role> Roles { get; set; } = null!;
    public virtual DbSet<Status> Statuses { get; set; } = null!;
    public virtual DbSet<StatusType> StatusTypes { get; set; } = null!;
    public virtual DbSet<Table> Tables { get; set; } = null!;
    public virtual DbSet<User> Users { get; set; } = null!;
    public virtual DbSet<UserRefreshToken> UserRefreshTokens { get; set; } = null!;
    public virtual DbSet<UserRestaurant> UserRestaurants { get; set; } = null!;
    public virtual DbSet<RolePermission> RolePermissions { get; set; } = null!;
    public DbSet<Wallet> Wallets { get; set; } = null!;

    public DbSet<WalletTransaction> WalletTransactions { get; set; } = null!;

    public DbSet<WithdrawRequest> WithdrawRequests { get; set; } = null!;

    public DbSet<AuditLog> AuditLogs { get; set; } = null!;

    public DbSet<Notification> Notifications { get; set; } = null!;

    public DbSet<Review> Reviews { get; set; } = null!;

    public DbSet<RestaurantContract> RestaurantContracts { get; set; } = null!;
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Automatically discovers all IEntityTypeConfiguration<T> configurations.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ECafeDbContext).Assembly);

        StatusTypeSeeder.Seed(modelBuilder);
        StatusSeeder.Seed(modelBuilder);
        PermissionSeeder.Seed(modelBuilder);
        RoleSeeder.Seed(modelBuilder);
        RolePermissionSeeder.Seed(modelBuilder);

        ApplySoftDeleteQueryFilters(modelBuilder);

        // Call scaffold partial mappings from here when present.
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

    public override int SaveChanges()
    {
        ApplyAuditInformation();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditInformation()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDelete softDeleteEntity)
            {
                entry.State = EntityState.Modified;
                softDeleteEntity.IsDeleted = true;

                if (entry.Entity is IAuditable auditableDeleted)
                {
                    entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                    auditableDeleted.UpdatedAt = now;
                }
            }

            if (entry.Entity is IAuditable auditable)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        auditable.CreatedAt = now;
                        auditable.UpdatedAt = null;
                        break;

                    case EntityState.Modified:
                        entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                        auditable.UpdatedAt = now;
                        break;
                }
            }
        }
    }

    private static void ApplySoftDeleteQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
                continue;

            // e => !e.IsDeleted
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var isDeleted = Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
            var filter = Expression.Lambda(Expression.Equal(isDeleted, Expression.Constant(false)), parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }
}

