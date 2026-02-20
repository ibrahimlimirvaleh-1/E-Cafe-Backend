using System.Linq.Expressions;
using ECafe.Domain.Entities;
using ECafe.Domain.Entities.Base;
using ECafe.Infrastructure.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ECafe.Infrastructure.Context;

public partial class ECafeDbContext : DbContext
{
    public ECafeDbContext(DbContextOptions<ECafeDbContext> options) : base(options) { }

    public virtual DbSet<Category> Categories { get; set; } = null!;
    public virtual DbSet<Item> Items { get; set; } = null!;
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
    public virtual DbSet<UserRestaurant> UserRestaurants { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Bütün IEntityTypeConfiguration<T> config-ləri avtomatik tapır
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ECafeDbContext).Assembly);

        StatusTypeSeeder.Seed(modelBuilder);
        StatusSeeder.Seed(modelBuilder);
        PermissionSeeder.Seed(modelBuilder);
        RoleSeeder.Seed(modelBuilder);

        ApplySoftDeleteQueryFilters(modelBuilder);

        // əgər scaffold partial mapping-in varsa, buradan çağır:
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

        foreach (EntityEntry<IAuditable> entry in ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = null;
                    break;

                case EntityState.Modified:
                    // CreatedAt dəyişməsin
                    entry.Property(x => x.CreatedAt).IsModified = false;
                    entry.Entity.UpdatedAt = now;
                    break;
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
