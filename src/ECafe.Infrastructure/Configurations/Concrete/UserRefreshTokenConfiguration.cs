using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class UserRefreshTokenConfiguration : DbEntityConfig<UserRefreshToken>
    {
        public override void Configure(EntityTypeBuilder<UserRefreshToken> builder)
        {
            builder.HasKey(e => e.Id).HasName("user_refresh_tokens_pkey");

            builder.ToTable("user_refresh_tokens", "auth");

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.UserId).HasColumnName("user_id");
            builder.Property(e => e.TokenHash)
                .HasMaxLength(64)
                .HasColumnName("token_hash");
            builder.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            builder.Property(e => e.RevokedAt).HasColumnName("revoked_at");
            builder.Property(e => e.ReplacedByTokenHash)
                .HasMaxLength(64)
                .HasColumnName("replaced_by_token_hash");
            builder.Property(e => e.CreatedByIp)
                .HasMaxLength(50)
                .HasColumnName("created_by_ip");
            builder.Property(e => e.RevokedByIp)
                .HasMaxLength(50)
                .HasColumnName("revoked_by_ip");
            builder.Property(e => e.UserAgent)
                .HasMaxLength(500)
                .HasColumnName("user_agent");

            builder.HasIndex(e => e.TokenHash, "user_refresh_tokens_token_hash_key")
                .IsUnique();

            builder.HasIndex(e => new { e.UserId, e.ExpiresAt }, "IX_user_refresh_tokens_user_id_expires_at");

            builder.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("user_refresh_tokens_user_id_fkey");
        }
    }
}
