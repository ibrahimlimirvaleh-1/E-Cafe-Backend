using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Domain.Enums;

namespace ECafe.Application.Services.ContentLocalization.Abstract;

public interface IContentTranslationAccessService
{
    Task<ContentTranslationSource> RequireAsync(ContentEntityType type, int id, CancellationToken cancellationToken);
}
