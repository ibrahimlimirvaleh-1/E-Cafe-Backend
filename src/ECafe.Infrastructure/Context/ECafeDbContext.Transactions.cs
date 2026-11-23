using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Context;

public partial class ECafeDbContext
{
    /// <summary>Açılmış hesabat transaction-ını PostgreSQL-də yalnız oxuma rejiminə keçirir; başqa provayderdə əvvəlki no-op davranışını saxlayır.</summary>
    internal Task SetCurrentTransactionReadOnlyAsync(CancellationToken cancellationToken)
        => Database.IsNpgsql()
            ? Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken)
            : Task.CompletedTask;
}
