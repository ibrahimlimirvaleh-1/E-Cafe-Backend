using ECafe.Domain.Entities;
using ECafe.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class KeywordTranslationConfiguration : IEntityTypeConfiguration<KeywordTranslation>
{
    public void Configure(EntityTypeBuilder<KeywordTranslation> builder)
    {
        builder.ToTable("keyword_translations", "core");
        builder.HasKey(translation => new { translation.KeywordId, translation.LanguageId });
        builder.Property(translation => translation.Value).HasMaxLength(TranslationPolicy.MaxTextLength).IsRequired();
        builder.HasOne(translation => translation.Keyword).WithMany(keyword => keyword.Translations)
            .HasForeignKey(translation => translation.KeywordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(translation => translation.Language).WithMany()
            .HasForeignKey(translation => translation.LanguageId).OnDelete(DeleteBehavior.Restrict);
    }
}
