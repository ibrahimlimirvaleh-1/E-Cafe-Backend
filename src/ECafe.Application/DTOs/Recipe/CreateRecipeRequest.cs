namespace ECafe.Application.DTOs.Recipe
{
    public class CreateRecipeRequest
    {
        public int InventoryItemId { get; set; }
        public decimal Quantity { get; set; }
        public int UnitId { get; set; }
    }
}
