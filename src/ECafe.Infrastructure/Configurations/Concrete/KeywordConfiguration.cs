using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class KeywordConfiguration : IEntityTypeConfiguration<Keyword>
{
    public void Configure(EntityTypeBuilder<Keyword> builder)
    {
        builder.ToTable("keywords", "core");
        builder.HasKey(keyword => keyword.Id);
        builder.Property(keyword => keyword.Key).HasMaxLength(512).IsRequired();
        builder.HasIndex(keyword => keyword.Key).IsUnique();
    }
}
