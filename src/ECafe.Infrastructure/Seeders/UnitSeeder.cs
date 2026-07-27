using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Shared.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Seeders
{
    public static class UnitSeeder
    {
        public static void Seed(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Unit>().HasData(
                Unit(UnitCode.Kilogram, "kg", null, 1m),
                Unit(UnitCode.Gram, "g", UnitCode.Kilogram, 0.001m),
                Unit(UnitCode.Liter, "l", null, 1m),
                Unit(UnitCode.Milliliter, "ml", UnitCode.Liter, 0.001m),
                Unit(UnitCode.Piece, "pcs", null, 1m));
        }

        private static Unit Unit(
            UnitCode unit,
            string code,
            UnitCode? baseUnit,
            decimal conversionRateToBase)
        {
            return new Unit
            {
                Id = (int)unit,
                Name = unit.GetName(),
                Code = code,
                BaseUnitId = baseUnit.HasValue ? (int)baseUnit.Value : null,
                ConversionRateToBase = conversionRateToBase,
                CreatedAt = DateTime.MinValue,
                CreatedBy = string.Empty,
                IsDeleted = false
            };
        }
    }
}
