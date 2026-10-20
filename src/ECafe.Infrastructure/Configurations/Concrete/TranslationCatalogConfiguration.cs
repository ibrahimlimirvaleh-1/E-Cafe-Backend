using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class TranslationCatalogConfiguration : IEntityTypeConfiguration<TranslationCatalog>
{
    public void Configure(EntityTypeBuilder<TranslationCatalog> builder)
    {
        builder.ToTable("translation_catalog", "core");
        builder.HasKey(catalog => catalog.Id);
        builder.Property(catalog => catalog.Id).ValueGeneratedNever();
        builder.Property(catalog => catalog.Revision).IsConcurrencyToken();
        builder.HasOne<TranslationRelease>().WithMany().HasForeignKey(catalog => catalog.PublishedReleaseId).OnDelete(DeleteBehavior.Restrict);
    }
}
