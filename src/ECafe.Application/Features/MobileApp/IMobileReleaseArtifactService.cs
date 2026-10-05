namespace ECafe.Application.Features.MobileApp;

public sealed record VerifiedMobileRelease(
    Stream Content,
    string Version,
    int VersionCode,
    long SizeBytes,
    string Sha256) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public interface IMobileReleaseArtifactService
{
    Task<VerifiedMobileRelease?> OpenVerifiedAsync(CancellationToken cancellationToken);
}
