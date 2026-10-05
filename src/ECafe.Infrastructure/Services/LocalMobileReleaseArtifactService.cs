using System.Security.Cryptography;
using ECafe.Application.Features.MobileApp;
using Microsoft.Extensions.Configuration;

namespace ECafe.Infrastructure.Services;

public sealed class LocalMobileReleaseArtifactService(IConfiguration configuration) : IMobileReleaseArtifactService
{
    public async Task<VerifiedMobileRelease?> OpenVerifiedAsync(CancellationToken cancellationToken)
    {
        var release = configuration.GetSection("MobileApp:Release");
        var path = release["ApkPath"];
        var version = release["Version"];
        var versionCode = release.GetValue<int>("VersionCode");
        var sizeBytes = release.GetValue<long>("SizeBytes");
        var expectedHash = release["Sha256"];
        if (!release.GetValue<bool>("Ready") ||
            string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
            string.IsNullOrWhiteSpace(version) || versionCode <= 0 || sizeBytes <= 0 ||
            expectedHash is null || expectedHash.Length != 64 || !expectedHash.All(Uri.IsHexDigit))
            return null;

        FileStream stream;
        var handedOff = false;
        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 81920, options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        try
        {
            if (stream.Length != sizeBytes)
                return null;

            var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                return null;

            stream.Position = 0;
            handedOff = true;
            return new VerifiedMobileRelease(stream, version, versionCode, sizeBytes,
                actualHash.ToLowerInvariant());
        }
        finally
        {
            if (!handedOff)
                await stream.DisposeAsync();
        }
    }
}
