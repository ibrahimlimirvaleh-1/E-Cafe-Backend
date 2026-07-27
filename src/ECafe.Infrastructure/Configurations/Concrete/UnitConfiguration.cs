using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class UnitConfiguration : DbEntityConfig<Unit>
    {
        public override void Configure(EntityTypeBuilder<Unit> builder)
        {
            builder.HasKey(e => e.Id).HasName("units_pkey");

            builder.ToTable("units", "inventory");

            builder.HasIndex(e => e.Code, "units_code_key").IsUnique();
            builder.HasIndex(e => e.Name, "units_name_key").IsUnique();

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
            builder.Property(e => e.Code)
                .HasMaxLength(20)
                .HasColumnName("code");
            builder.Property(e => e.BaseUnitId).HasColumnName("base_unit_id");
            builder.Property(e => e.ConversionRateToBase)
                .HasPrecision(18, 6)
                .HasDefaultValue(1m)
                .HasColumnName("conversion_rate_to_base");

            builder.HasOne(e => e.BaseUnit)
                .WithMany()
                .HasForeignKey(e => e.BaseUnitId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("units_base_unit_id_fkey");
        }
    }
}
