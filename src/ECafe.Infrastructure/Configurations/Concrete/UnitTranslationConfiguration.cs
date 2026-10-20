using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class UnitTranslationConfiguration : IEntityTypeConfiguration<UnitTranslation>
{
    public void Configure(EntityTypeBuilder<UnitTranslation> builder)
    {
        ContentTranslationConfiguration.Configure(builder, "unit_translations", "inventory", nameof(UnitTranslation.UnitId), 50);
        builder.HasOne(row => row.Unit).WithMany(owner => owner.Translations).HasForeignKey(row => row.UnitId).OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(row => !row.Unit.IsDeleted && row.Language.IsActive);
    }
}
