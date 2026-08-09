using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public class LoginAttemptConfiguration : DbEntityConfig<LoginAttempt>
{
    public override void Configure(EntityTypeBuilder<LoginAttempt> builder)
    {
        builder.HasKey(x => x.Id).HasName("login_attempts_pkey");

        builder.ToTable("login_attempts", "auth");

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Email)
            .HasMaxLength(100)
            .HasColumnName("email");
        builder.Property(x => x.IsSuccessful).HasColumnName("is_successful");
        builder.Property(x => x.FailureReason)
            .HasMaxLength(100)
            .HasColumnName("failure_reason");
        builder.Property(x => x.IpAddress)
            .HasMaxLength(64)
            .HasColumnName("ip_address");
        builder.Property(x => x.UserAgent)
            .HasMaxLength(512)
            .HasColumnName("user_agent");
        builder.Property(x => x.AttemptedAt).HasColumnName("attempted_at");
        builder.Property(x => x.LockoutUntil).HasColumnName("lockout_until");

        builder.HasIndex(x => new { x.Email, x.AttemptedAt })
            .HasDatabaseName("IX_login_attempts_email_attempted_at");
        builder.HasIndex(x => new { x.Email, x.LockoutUntil })
            .HasDatabaseName("IX_login_attempts_email_lockout_until");
        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("IX_login_attempts_user_id");

        builder.HasOne(x => x.User)
            .WithMany(x => x.LoginAttempts)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("login_attempts_user_id_fkey");
    }
}
