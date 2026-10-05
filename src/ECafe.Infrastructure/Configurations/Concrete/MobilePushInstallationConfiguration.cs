using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class MobilePushInstallationConfiguration : IEntityTypeConfiguration<MobilePushInstallation>
{
    public void Configure(EntityTypeBuilder<MobilePushInstallation> builder)
    {
        builder.ToTable("mobile_push_installations", "core");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.SessionId).HasMaxLength(32).HasColumnName("session_id");
        builder.Property(x => x.ExpoProjectId).HasColumnName("expo_project_id");
        builder.Property(x => x.TokenHash).HasMaxLength(64).HasColumnName("token_hash");
        builder.Property(x => x.ProtectedToken).HasMaxLength(1024).HasColumnName("protected_token");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.RegisteredAt).HasColumnName("registered_at");
        builder.Property(x => x.DeactivatedAt).HasColumnName("deactivated_at");
        builder.HasIndex(x => x.TokenHash)
            .IsUnique()
            .HasFilter("is_active = true");
        builder.HasIndex(x => new { x.UserId, x.SessionId, x.IsActive });
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
