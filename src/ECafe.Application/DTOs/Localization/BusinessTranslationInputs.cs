namespace ECafe.Application.DTOs.Localization;

public sealed class RestaurantTranslationInput
{
    public string LanguageCode { get; set; } = null!;
    public string? Name { get; set; }
    public string? BranchName { get; set; }
    public string? Location { get; set; }
}

public sealed class RestaurantGroupTranslationInput
{
    public string LanguageCode { get; set; } = null!;
    public string? Name { get; set; }
}
