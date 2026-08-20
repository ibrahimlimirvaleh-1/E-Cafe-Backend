using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class RestaurantContractConfiguration : DbEntityConfig<RestaurantContract>
    {
        public override void Configure(EntityTypeBuilder<RestaurantContract> builder)
        {
            builder.HasKey(e => e.Id).HasName("restaurant_contracts_pkey");

            builder.ToTable("restaurant_contracts", "core");

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");
            builder.Property(e => e.ContractNumber)
                .HasMaxLength(64)
                .HasColumnName("contract_number");
            builder.Property(e => e.StartDate).HasColumnName("start_date");
            builder.Property(e => e.EndDate).HasColumnName("end_date");
            builder.Property(e => e.Amount)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m)
                .HasColumnName("amount");
            builder.Property(e => e.CommissionPercent)
                .HasPrecision(5, 2)
                .HasColumnName("commission_percent");
            builder.Property(e => e.StaffSettlementPeriod).HasColumnName("staff_settlement_period");
            builder.Property(e => e.ExpiryReminderDaysBefore)
                .HasDefaultValue(1)
                .HasColumnName("expiry_reminder_days_before");
            builder.Property(e => e.ExpiryReminderAt).HasColumnName("expiry_reminder_at");
            builder.Property(e => e.ExpiryReminderSentAt).HasColumnName("expiry_reminder_sent_at");
            builder.Property(e => e.PaymentPolicyId)
                .HasDefaultValue((int)ContractPaymentPolicy.OnlineOnly)
                .HasColumnName("payment_policy_id");
            builder.Property(e => e.StatusId).HasColumnName("status_id");
            builder.Property(e => e.FileId).HasColumnName("file_id");
            builder.Property(e => e.SignedAt).HasColumnName("signed_at");
            builder.Property(e => e.SignedByUserId).HasColumnName("signed_by_user_id");

            builder.HasIndex(e => e.ContractNumber)
                .IsUnique()
                .HasDatabaseName("restaurant_contracts_contract_number_key");

            builder.HasIndex(e => e.RestaurantId)
                .IsUnique()
                .HasFilter("status_id = 6003 AND \"IsDeleted\" = false")
                .HasDatabaseName("restaurant_contracts_one_active_per_restaurant_key");

            builder.HasIndex(e => new { e.StatusId, e.ExpiryReminderAt, e.ExpiryReminderSentAt })
                .HasDatabaseName("ix_restaurant_contracts_expiry_reminder");

            builder.HasOne(e => e.Restaurant)
                .WithMany(e => e.Contracts)
                .HasForeignKey(e => e.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("restaurant_contracts_restaurant_id_fkey");

            builder.HasOne(e => e.Status)
                .WithMany(e => e.RestaurantContracts)
                .HasForeignKey(e => e.StatusId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("restaurant_contracts_status_id_fkey");

            builder.HasOne(e => e.File)
                .WithMany(e => e.RestaurantContracts)
                .HasForeignKey(e => e.FileId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("restaurant_contracts_file_id_fkey");

            builder.HasOne(e => e.SignedByUser)
                .WithMany(e => e.SignedRestaurantContracts)
                .HasForeignKey(e => e.SignedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("restaurant_contracts_signed_by_user_id_fkey");
        }
    }
}
