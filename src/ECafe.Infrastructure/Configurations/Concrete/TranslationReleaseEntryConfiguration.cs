using ECafe.Domain.Entities;
using ECafe.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class TranslationReleaseEntryConfiguration : IEntityTypeConfiguration<TranslationReleaseEntry>
{
    public void Configure(EntityTypeBuilder<TranslationReleaseEntry> builder)
    {
        builder.ToTable("translation_release_entries", "core");
        builder.HasKey(entry => new { entry.TranslationReleaseId, entry.KeywordId, entry.LanguageId });
        builder.Property(entry => entry.Value).HasMaxLength(TranslationPolicy.MaxTextLength).IsRequired();
        builder.HasOne(entry => entry.TranslationRelease).WithMany(release => release.Entries)
            .HasForeignKey(entry => entry.TranslationReleaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entry => entry.Keyword).WithMany()
            .HasForeignKey(entry => entry.KeywordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entry => entry.Language).WithMany()
            .HasForeignKey(entry => entry.LanguageId).OnDelete(DeleteBehavior.Restrict);
    }
}
