using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Context;

/// <summary>PostgreSQL kilid SQL-ini bir yerdə saxlayır; transaction-ı çağıran xidmət açır və tamamlayır.</summary>
internal sealed class PostgresTransactionLocks(ECafeDbContext context)
{
    /// <summary>Auth və rezervasiya sorğularını eyni istifadəçi sətrində transaction sonunadək ardıcıllaşdırır.</summary>
    internal Task LockUserAsync(int userId, CancellationToken cancellationToken = default)
        => context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM auth.users WHERE id = {userId} FOR UPDATE", cancellationToken);

    /// <summary>Çağıranın mövcud iki hissəli kilid açarını dəyişmədən transaction-scoped advisory kilidi alır.</summary>
    internal Task LockAdvisoryAsync(int namespaceKey, int resourceKey, CancellationToken cancellationToken = default)
        => context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({namespaceKey}, {resourceKey})", cancellationToken);

    /// <summary>Yalnız seçilmiş restoranın stok sətirlərini sabit ID sırası ilə kilidləyir; ID massivi SQL parametridir.</summary>
    internal Task LockInventoryRowsAsync(int restaurantId, int[] ids, CancellationToken cancellationToken = default)
        => context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM inventory.inventory_items WHERE restaurant_id = {restaurantId} AND id = ANY({ids}) ORDER BY id FOR UPDATE",
            cancellationToken);
}
