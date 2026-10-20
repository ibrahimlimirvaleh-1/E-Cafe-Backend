using ECafe.Application.DTOs.ContentLocalization;

namespace ECafe.Application.Services.ContentLocalization.Abstract;

public interface IContentTranslationService
{
    Task<IReadOnlyList<ContentLanguageResponse>> GetLanguagesAsync(CancellationToken cancellationToken);
    Task<ContentTranslationsResponse> GetAsync(string entityType, int id, CancellationToken cancellationToken);
    Task<ContentTranslationsResponse> SaveAsync(string entityType, int id, string language,
        SaveContentTranslationRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(string entityType, int id, string language, long? expectedRevision, CancellationToken cancellationToken);
}

public interface IContentTranslationReader
{
    Task LocalizeAsync(object response, CancellationToken cancellationToken, ContentReadContext? context = null);
}

public sealed record ContentReadContext(int? RestaurantId = null, bool UnitLookup = false);
