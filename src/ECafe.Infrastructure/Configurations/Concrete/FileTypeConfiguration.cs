using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class FileTypeConfiguration : DbEntityConfig<FileType>
    {
        public override void Configure(EntityTypeBuilder<FileType> builder)
        {
            builder.HasKey(e => e.Id).HasName("file_types_pkey");

            builder.ToTable("file_types", "common");

            builder.HasIndex(e => e.Code, "file_types_code_key").IsUnique();
            builder.HasIndex(e => e.Name, "file_types_name_key").IsUnique();

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            builder.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            builder.Property(e => e.IsPublic).HasColumnName("is_public");
            builder.Property(e => e.AllowedExtensions)
                .HasMaxLength(200)
                .HasColumnName("allowed_extensions");
            builder.Property(e => e.AllowedMimeTypes)
                .HasMaxLength(500)
                .HasColumnName("allowed_mime_types");
            builder.Property(e => e.MaxSizeMb).HasColumnName("max_size_mb");
        }
    }
}
