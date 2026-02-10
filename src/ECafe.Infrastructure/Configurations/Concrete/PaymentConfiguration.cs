using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class PaymentConfiguration : DbEntityConfig<Payment>
    {
        public override void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.HasKey(e => e.Id).HasName("payments_pkey");

            builder.ToTable("payments", "billing");

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.Amount)
                .HasPrecision(10, 2)
                .HasColumnName("amount");
            builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            builder.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
            builder.Property(e => e.OrderId).HasColumnName("order_id");
            builder.Property(e => e.PaidAt).HasColumnName("paid_at");
            builder.Property(e => e.PaymentMethodId).HasColumnName("payment_method_id");
            builder.Property(e => e.PaymentStatusId).HasColumnName("payment_status_id");
            builder.Property(e => e.ProviderRef).HasColumnName("provider_ref");
            builder.Property(e => e.ReservationId).HasColumnName("reservation_id");
            builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");

            builder.HasOne(d => d.CreatedByUser).WithMany(p => p.Payments)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("payments_created_by_user_id_fkey");

            builder.HasOne(d => d.Order).WithMany(p => p.Payments)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("payments_order_id_fkey");

            builder.HasOne(d => d.PaymentMethod).WithMany(p => p.PaymentPaymentMethods)
                .HasForeignKey(d => d.PaymentMethodId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("payments_payment_method_id_fkey");

            builder.HasOne(d => d.PaymentStatus).WithMany(p => p.PaymentPaymentStatuses)
                .HasForeignKey(d => d.PaymentStatusId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("payments_payment_status_id_fkey");

            builder.HasOne(d => d.Reservation).WithMany(p => p.Payments)
                .HasForeignKey(d => d.ReservationId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("payments_reservation_id_fkey");

            builder.HasOne(d => d.Restaurant).WithMany(p => p.Payments)
                .HasForeignKey(d => d.RestaurantId)
                .HasConstraintName("payments_restaurant_id_fkey");
        }
    }
}
