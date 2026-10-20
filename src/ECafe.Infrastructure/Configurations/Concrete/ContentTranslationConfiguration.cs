using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

internal static class ContentTranslationConfiguration
{
    /// <summary>Hər tərcüməni ayrıca cədvələ bağlayır; entity/dil cütünü unikal və reviziyanı optimist kilid edir.</summary>
    internal static void Configure<T>(EntityTypeBuilder<T> builder, string table, string schema, string ownerKey, int nameLength)
        where T : EntityTranslation
    {
        builder.HasBaseType((Type?)null);
        builder.ToTable(table, schema);
        builder.HasKey(ownerKey, nameof(EntityTranslation.LanguageId));
        builder.Property<string?>("Name").HasMaxLength(nameLength);
        builder.Property(row => row.Revision).IsConcurrencyToken();
        builder.HasOne(row => row.Language).WithMany().HasForeignKey(row => row.LanguageId).OnDelete(DeleteBehavior.Restrict);
    }
}
