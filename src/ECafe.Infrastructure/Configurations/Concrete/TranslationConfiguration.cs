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
        builder.Property(catalog => catalog.DraftJson).HasColumnType("jsonb");
        builder.Property(catalog => catalog.Revision).IsConcurrencyToken();
        builder.HasOne<TranslationRelease>().WithMany().HasForeignKey(catalog => catalog.PublishedReleaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasData(new TranslationCatalog { Id = 1, Revision = 0, DraftJson = "{}" });
    }
}

public sealed class TranslationReleaseConfiguration : IEntityTypeConfiguration<TranslationRelease>
{
    public void Configure(EntityTypeBuilder<TranslationRelease> builder)
    {
        builder.ToTable("translation_releases", "core");
        builder.HasKey(release => release.Id);
        builder.Property(release => release.MessagesJson).HasColumnType("jsonb");
        builder.HasOne<User>().WithMany().HasForeignKey(release => release.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TranslationRelease>().WithMany().HasForeignKey(release => release.SourceReleaseId).OnDelete(DeleteBehavior.Restrict);
    }
}
