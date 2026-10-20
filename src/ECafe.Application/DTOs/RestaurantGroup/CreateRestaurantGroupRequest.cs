namespace ECafe.Application.DTOs.RestaurantGroup
{
    public class CreateRestaurantGroupRequest
    {
        public List<ECafe.Application.DTOs.Localization.RestaurantGroupTranslationInput>? Translations { get; set; }
        public string Name { get; set; } = null!;

        public string? LegalName { get; set; }

        public string? Email { get; set; }
    }
}
