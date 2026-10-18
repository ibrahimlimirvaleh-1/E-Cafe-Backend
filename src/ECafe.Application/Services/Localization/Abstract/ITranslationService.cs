using ECafe.Application.DTOs.Localization;
using ECafe.Domain.Policies;

namespace ECafe.Application.Services.Localization.Abstract;

public interface ITranslationService
{
    Task<TranslationSnapshot> GetPublishedAsync(CancellationToken cancellationToken);
    Task<TranslationDraftResponse> GetDraftAsync(CancellationToken cancellationToken);
    Task<TranslationDraftResponse> SaveDraftAsync(SaveTranslationDraftRequest request, CancellationToken cancellationToken);
    Task<TranslationReleaseResponse> PublishAsync(long expectedRevision, int? rollbackReleaseId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TranslationReleaseResponse>> GetReleasesAsync(CancellationToken cancellationToken);
    Task<LanguagePreferenceResponse> GetPreferenceAsync(CancellationToken cancellationToken);
    Task<LanguagePreferenceResponse> SavePreferenceAsync(string language, CancellationToken cancellationToken);
}

public interface ITranslationCache
{
    Task<TranslationSnapshot> GetAsync(Func<CancellationToken, Task<TranslationSnapshot>> loader, CancellationToken cancellationToken);
    void Invalidate();
}

public interface ITextLocalizer
{
    string Get(string key, string fallback, object? parameters = null);
    string TranslateLiteral(string value);
    TranslationMatch? ResolveSource(string value);
    TranslationMatch? ResolveNotificationSource(string value);
}
