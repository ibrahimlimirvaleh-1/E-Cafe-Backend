namespace ECafe.Application.DTOs.ContentLocalization;

public sealed record ContentLanguageResponse(int Id, string Code, string Name, bool IsDefault);

public sealed record ContentTranslationValue(string? Name = null, string? Description = null,
    string? Location = null, string? BranchName = null, string? UnavailableReason = null);

public sealed record ContentTranslationEntry(string LanguageCode, string? Name, string? Description,
    string? Location, string? BranchName, string? UnavailableReason, long Revision);

public sealed record ContentTranslationsResponse(string EntityType, int Id, int? RestaurantId,
    IReadOnlyList<ContentTranslationEntry> Translations);

public sealed class SaveContentTranslationRequest
{
    public long? ExpectedRevision { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? BranchName { get; set; }
    public string? UnavailableReason { get; set; }
}
