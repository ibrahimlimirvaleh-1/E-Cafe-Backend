namespace ECafe.Application.DTOs.Category
{
    public class UpdateCategoryRequest
    {
        public List<CategoryTranslationRequest>? Translations { get; set; }
        public string Name { get; set; } = null!;
        public int? SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
