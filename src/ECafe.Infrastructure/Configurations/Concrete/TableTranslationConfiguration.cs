using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class TableTranslationConfiguration : IEntityTypeConfiguration<TableTranslation>
{
    public void Configure(EntityTypeBuilder<TableTranslation> builder)
    {
        ContentTranslationConfiguration.Configure(builder, "table_translations", "ops", nameof(TableTranslation.TableId), 50);
        builder.HasOne(row => row.Table).WithMany(owner => owner.Translations).HasForeignKey(row => row.TableId).OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(row => !row.Table.IsDeleted && !row.Table.Restaurant.IsDeleted && row.Language.IsActive);
    }
}
