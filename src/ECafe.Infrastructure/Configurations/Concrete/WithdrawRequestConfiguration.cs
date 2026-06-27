using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations;

public class WithdrawRequestConfiguration
    : IEntityTypeConfiguration<WithdrawRequest>
{
    public void Configure(EntityTypeBuilder<WithdrawRequest> builder)
    {
        builder.ToTable("withdraw_requests", "billing");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.RejectReason)
            .HasMaxLength(500);

        builder.HasOne(x => x.Wallet)
            .WithMany(x => x.WithdrawRequests)
            .HasForeignKey(x => x.WalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Status)
            .WithMany()
            .HasForeignKey(x => x.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedByUser)
            .WithMany()
            .HasForeignKey(x => x.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}