using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class UserConfiguration : DbEntityConfig<User>
    {
        public override void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(e => e.Id).HasName("users_pkey");

            builder.ToTable("users", "auth");

            builder.HasIndex(e => e.Email, "users_email_key").IsUnique();

            builder.HasIndex(e => e.Phone, "users_phone_key").IsUnique();

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.Email)
                .HasMaxLength(100)
                .HasColumnName("email");
            builder.Property(e => e.FileId).HasColumnName("file_id");
            builder.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            builder.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
            builder.Property(e => e.PasswordHash)
                .HasMaxLength(200)
                .HasColumnName("password_hash");
            builder.Property(e => e.Phone)
                .HasMaxLength(50)
                .HasColumnName("phone");
            builder.Property(e => e.Rating)
                .HasPrecision(3, 2)
                .HasColumnName("rating");
            builder.Property(e => e.Surname)
                .HasMaxLength(50)
                .HasColumnName("surname");

            builder.HasOne(d => d.File).WithMany(p => p.Users)
                .HasForeignKey(d => d.FileId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("users_file_id_fkey");

        }
    }
}
