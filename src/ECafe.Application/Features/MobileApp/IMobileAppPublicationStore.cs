namespace ECafe.Application.Features.MobileApp;

public interface IMobileAppPublicationStore
{
    Task<bool> IsPublicDownloadEnabledAsync(CancellationToken cancellationToken);
    Task SetPublicDownloadEnabledAsync(bool enabled, CancellationToken cancellationToken);
}
