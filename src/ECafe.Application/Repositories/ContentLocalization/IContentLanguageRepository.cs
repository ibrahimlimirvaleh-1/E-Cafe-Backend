namespace ECafe.Application.Repositories.ContentLocalization;

public interface IContentLanguageRepository
{
    Task<IReadOnlyList<ContentLanguage>> GetLanguagesAsync(CancellationToken cancellationToken);
}
