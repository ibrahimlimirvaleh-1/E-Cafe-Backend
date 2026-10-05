using ECafe.Application.Features.MobileApp;
using ECafe.Domain.Entities;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories;

public sealed class MobileAppPublicationStore(ECafeDbContext context) : IMobileAppPublicationStore
{
    private const int SingletonId = 1;

    public async Task<bool> IsPublicDownloadEnabledAsync(CancellationToken cancellationToken)
        => await context.MobileAppPublications
            .Where(x => x.Id == SingletonId)
            .Select(x => (bool?)x.PublicDownloadEnabled)
            .SingleOrDefaultAsync(cancellationToken) ?? false;

    public async Task SetPublicDownloadEnabledAsync(bool enabled, CancellationToken cancellationToken)
    {
        var publication = await context.MobileAppPublications
            .SingleOrDefaultAsync(x => x.Id == SingletonId, cancellationToken);
        if (publication is null)
        {
            publication = new MobileAppPublication { Id = SingletonId };
            context.MobileAppPublications.Add(publication);
        }

        publication.PublicDownloadEnabled = enabled;
        await context.SaveChangesAsync(cancellationToken);
    }
}
