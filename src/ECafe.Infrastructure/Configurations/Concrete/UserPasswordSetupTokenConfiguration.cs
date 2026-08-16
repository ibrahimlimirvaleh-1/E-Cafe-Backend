using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class UserPasswordSetupTokenConfiguration : IEntityTypeConfiguration<UserPasswordSetupToken>
    {
        public void Configure(EntityTypeBuilder<UserPasswordSetupToken> builder)
        {
            builder.ToTable("user_password_setup_tokens", "auth");

            builder.HasKey(x => x.Id).HasName("user_password_setup_tokens_pkey");

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.TokenHash)
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnName("token_hash");

            builder.Property(x => x.ExpiresAt)
                .IsRequired()
                .HasColumnName("expires_at");

            builder.Property(x => x.UsedAt)
                .IsRequired(false)
                .HasColumnName("used_at");

            builder.HasIndex(x => x.TokenHash, "user_password_setup_tokens_token_hash_key")
                .IsUnique();

            builder.HasIndex(x => new { x.UserId, x.ExpiresAt }, "IX_user_password_setup_tokens_user_id_expires_at");

            builder.HasOne(x => x.User)
                .WithMany(x => x.PasswordSetupTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("user_password_setup_tokens_user_id_fkey");
        }
    }
}
