namespace ECafe.Application.DTOs.Recipe
{
    public class UpdateRecipeRequest
    {
        public int InventoryItemId { get; set; }
        public decimal Quantity { get; set; }
        public int UnitId { get; set; }
        public bool IsActive { get; set; }
    }
}
