using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class RestaurantConfiguration : DbEntityConfig<Restaurant>
    {
        public override void Configure(EntityTypeBuilder<Restaurant> builder)
        {
            builder.HasKey(e => e.Id).HasName("restaurants_pkey");

            builder.ToTable("restaurants", "core");

            builder.Property(e => e.Id).HasColumnName("id");
            builder.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            builder.Property(e => e.Location)
                .HasMaxLength(200)
                .HasColumnName("location");
            builder.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
            builder.Property(e => e.Phone)
                .HasMaxLength(50)
                .HasColumnName("phone");
            builder.Property(e => e.RatingAverage)
                .HasPrecision(3, 2)
                .HasColumnName("rating_average");
            builder.Property(e => e.RatingCount)
                .HasDefaultValue(0)
                .HasColumnName("rating_count");

        }
    }
}
