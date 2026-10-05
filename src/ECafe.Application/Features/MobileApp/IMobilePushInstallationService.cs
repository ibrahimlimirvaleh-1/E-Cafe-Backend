namespace ECafe.Application.Features.MobileApp;

public sealed record RegisterMobilePushInstallationRequest(string ExpoPushToken, Guid ExpoProjectId);

public sealed record MobilePushInstallationResponse(Guid InstallationId, bool IsActive);

public interface IMobilePushInstallationService
{
    Task<MobilePushInstallationResponse> RegisterAsync(
        Guid installationId,
        RegisterMobilePushInstallationRequest request,
        CancellationToken cancellationToken);

    Task DeactivateAsync(Guid installationId, CancellationToken cancellationToken);
}
