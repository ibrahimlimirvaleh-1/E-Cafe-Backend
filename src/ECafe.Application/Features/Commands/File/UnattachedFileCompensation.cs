using ECafe.Shared.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ECafe.Application.Features.Commands.File;

public static class UnattachedFileCompensation
{
    /// <summary>Uğursuz upload axınının sərbəst faylını sorğunun cancellation-ından asılı olmadan silməyə çalışır; cleanup xətası əsas xətanı əvəz etmir.</summary>
    public static async Task TryDeleteAsync(ISender sender, ILogger logger, int fileId, string uploadKind)
    {
        try
        {
            await sender.Send(new DeleteFileCommand { FileId = fileId });
        }
        catch (Exception exception)
        {
            logger.LogWarning(SafeDiagnosticException.ForLogging(exception),
                "Could not compensate {UploadKind} upload for file {FileId}. The unattached-file cleanup worker will retry it.",
                uploadKind, fileId);
        }
    }
}
