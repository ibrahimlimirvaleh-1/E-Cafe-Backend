using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class OrderCancellationRequestConfiguration : IEntityTypeConfiguration<OrderCancellationRequest>
{
    public void Configure(EntityTypeBuilder<OrderCancellationRequest> builder)
    {
        builder.ToTable("order_cancellation_requests", "ops", table => table.HasCheckConstraint(
            "cancellation_stock_return_mode_ck", "\"StockReturnMode\" IS NULL OR \"StockReturnMode\" IN (1, 2, 3)"));
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Reason).HasMaxLength(500).IsRequired();
        builder.Property(request => request.DecisionReason).HasMaxLength(500);
        builder.HasOne(request => request.Order).WithOne(order => order.CancellationRequest)
            .HasForeignKey<OrderCancellationRequest>(request => request.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(request => request.OrderId).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(request => request.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(request => request.ForwardedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(request => request.ResolvedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
