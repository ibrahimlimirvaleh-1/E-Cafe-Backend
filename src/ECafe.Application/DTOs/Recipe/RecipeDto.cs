namespace ECafe.Application.DTOs.Recipe
{
    public class RecipeDto
    {
        public int Id { get; set; }
        public int RestaurantId { get; set; }

        public int ItemId { get; set; }
        public string ItemName { get; set; } = null!;

        public int InventoryItemId { get; set; }
        public string InventoryItemName { get; set; } = null!;

        public decimal Quantity { get; set; }

        public int UnitId { get; set; }
        public string UnitName { get; set; } = null!;
        public string UnitCode { get; set; } = null!;

        public bool IsActive { get; set; }
    }
}
