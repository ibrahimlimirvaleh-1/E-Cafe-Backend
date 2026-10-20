namespace ECafe.Application.DTOs.Category
{
    public class CreateCategoryRequest
    {
        public List<CategoryTranslationRequest>? Translations { get; set; }
        public int RestaurantId { get; set; }
        public string Name { get; set; } = null!;
        public int? SortOrder { get; set; }
    }
}
