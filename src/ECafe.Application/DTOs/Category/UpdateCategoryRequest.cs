namespace ECafe.Application.DTOs.Category
{
    public class UpdateCategoryRequest
    {
        public string Name { get; set; } = null!;
        public int? SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
