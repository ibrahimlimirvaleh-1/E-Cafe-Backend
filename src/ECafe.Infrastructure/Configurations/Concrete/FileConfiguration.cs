using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class FileConfiguration : DbEntityConfig<Domain.Entities.File>
    {
        public override void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.Entities.File> builder)
        {
            builder.HasKey(e => e.Id).HasName("files_pkey");

            builder.ToTable("files", "common");

            builder.HasIndex(e => e.Token, "files_token_key").IsUnique();

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.Token)
                .HasMaxLength(100)
                .HasColumnName("token");
            builder.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            builder.Property(e => e.Extension)
                .HasMaxLength(20)
                .HasColumnName("extension");
            builder.Property(e => e.Size).HasColumnName("size");
            builder.Property(e => e.Url)
                .HasMaxLength(500)
                .HasColumnName("url");

            builder.HasOne(e => e.Restaurant)
                .WithMany(r => r.Files);

        }
    }
}
