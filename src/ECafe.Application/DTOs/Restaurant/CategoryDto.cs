namespace ECafe.Application.DTOs.Restaurant
{
    public class CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;

        public string Slug { get; set; } = null!;

        public int SortOrder { get; set; }

        public bool IsActive { get; set; }
    }
}
