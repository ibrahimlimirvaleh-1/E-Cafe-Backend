using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class MobileAppPublicationConfiguration : IEntityTypeConfiguration<MobileAppPublication>
{
    public void Configure(EntityTypeBuilder<MobileAppPublication> builder)
    {
        builder.ToTable("mobile_app_publication", "core");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.PublicDownloadEnabled)
            .HasColumnName("public_download_enabled")
            .HasDefaultValue(false);
    }
}
