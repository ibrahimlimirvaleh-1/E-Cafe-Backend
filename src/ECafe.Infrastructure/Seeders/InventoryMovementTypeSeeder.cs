using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Shared.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Seeders
{
    public static class InventoryMovementTypeSeeder
    {
        public static void Seed(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InventoryMovementType>().HasData(
                MovementType(InventoryMovementTypeCode.Purchase, "Purchase", "New stock is purchased and added to inventory."),
                MovementType(InventoryMovementTypeCode.ManualIncrease, "ManualIncrease", "Manual inventory increase."),
                MovementType(InventoryMovementTypeCode.ManualDecrease, "ManualDecrease", "Manual inventory decrease."),
                MovementType(InventoryMovementTypeCode.OrderConsumption, "OrderConsumption", "Stock is consumed when an order item is prepared."),
                MovementType(InventoryMovementTypeCode.Waste, "Waste", "Damaged or wasted stock is removed from inventory."),
                MovementType(InventoryMovementTypeCode.StockReturn, "StockReturn", "Stock return movement."),
                MovementType(InventoryMovementTypeCode.Correction, "Correction", "Inventory balance correction after count or audit."));
        }

        private static InventoryMovementType MovementType(
            InventoryMovementTypeCode movementType,
            string code,
            string description)
        {
            return new InventoryMovementType
            {
                Id = (int)movementType,
                Name = movementType.GetName(),
                Code = code,
                Description = description,
                CreatedAt = DateTime.MinValue,
                CreatedBy = string.Empty,
                IsDeleted = false
            };
        }
    }
}
