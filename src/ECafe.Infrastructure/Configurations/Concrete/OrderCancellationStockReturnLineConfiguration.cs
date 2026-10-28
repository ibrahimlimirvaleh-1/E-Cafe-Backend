using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class OrderCancellationStockReturnLineConfiguration : IEntityTypeConfiguration<OrderCancellationStockReturnLine>
{
    public void Configure(EntityTypeBuilder<OrderCancellationStockReturnLine> builder)
    {
        builder.ToTable("order_cancellation_stock_return_lines", "ops", table => table.HasCheckConstraint(
            "cancellation_stock_return_quantity_ck", "\"ConsumedQuantity\" > 0 AND \"ReturnQuantity\" >= 0 AND \"ReturnQuantity\" <= \"ConsumedQuantity\""));
        builder.HasKey(line => line.Id);
        builder.Property(line => line.ConsumedQuantity).HasPrecision(18, 6);
        builder.Property(line => line.ReturnQuantity).HasPrecision(18, 6);
        builder.HasIndex(line => new { line.CancellationRequestId, line.InventoryItemId }).IsUnique();
        builder.HasOne(line => line.CancellationRequest).WithMany(request => request.StockReturnLines)
            .HasForeignKey(line => line.CancellationRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryItem>().WithMany().HasForeignKey(line => line.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Unit>().WithMany().HasForeignKey(line => line.UnitId).OnDelete(DeleteBehavior.Restrict);
    }
}
