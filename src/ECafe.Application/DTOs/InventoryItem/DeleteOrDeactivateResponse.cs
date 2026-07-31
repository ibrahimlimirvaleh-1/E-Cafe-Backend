namespace ECafe.Application.DTOs.InventoryItem
{
    public class DeleteOrDeactivateResponse
    {
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsActive { get; set; }
    }
}
