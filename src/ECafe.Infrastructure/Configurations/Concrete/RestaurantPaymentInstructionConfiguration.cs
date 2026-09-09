using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public class RestaurantPaymentInstructionConfiguration : DbEntityConfig<RestaurantPaymentInstruction>
{
    public override void Configure(EntityTypeBuilder<RestaurantPaymentInstruction> builder)
    {
        builder.HasKey(e => e.Id).HasName("restaurant_payment_instructions_pkey");

        builder.ToTable("restaurant_payment_instructions", "billing");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");
        builder.Property(e => e.Title)
            .HasMaxLength(100)
            .HasColumnName("title");
        builder.Property(e => e.PaymentMethod)
            .HasMaxLength(50)
            .HasColumnName("payment_method");
        builder.Property(e => e.DisplayText)
            .HasMaxLength(1000)
            .HasColumnName("display_text");
        builder.Property(e => e.IsActive)
            .HasDefaultValue(true)
            .HasColumnName("is_active");

        builder.HasIndex(e => new { e.RestaurantId, e.IsActive }, "restaurant_payment_instructions_restaurant_active_idx");

        builder.HasOne(e => e.Restaurant)
            .WithMany(e => e.PaymentInstructions)
            .HasForeignKey(e => e.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("restaurant_payment_instructions_restaurant_id_fkey");
    }
}
