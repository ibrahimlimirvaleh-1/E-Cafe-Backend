namespace ECafe.Application.DTOs.Category
{
    public class GetAllCategoryResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
    }
}
