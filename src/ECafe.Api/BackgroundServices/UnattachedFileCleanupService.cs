using ECafe.Application.Repositories.File;
using ECafe.Application.Services.MinIO.Abstracts;

namespace ECafe.Api.BackgroundServices
{
    public sealed class UnattachedFileCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<UnattachedFileCleanupService> _logger;
        private readonly TimeSpan _interval;
        private readonly TimeSpan _fileTtl;
        private const int BatchSize = 100;

        public UnattachedFileCleanupService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<UnattachedFileCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _interval = TimeSpan.FromMinutes(
                configuration.GetValue("FileCleanup:IntervalMinutes", 60));
            _fileTtl = TimeSpan.FromHours(
                configuration.GetValue("FileCleanup:UnattachedFileTtlHours", 24));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await CleanupAsync(stoppingToken);

            using var timer = new PeriodicTimer(_interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CleanupAsync(stoppingToken);
            }
        }

        private async Task CleanupAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var fileRepository = scope.ServiceProvider.GetRequiredService<IFileRepository>();
                var minioService = scope.ServiceProvider.GetRequiredService<IMinioService>();

                var cutoffUtc = DateTime.UtcNow.Subtract(_fileTtl);
                var files = await fileRepository.GetUnattachedOlderThanAsync(cutoffUtc, BatchSize);

                foreach (var file in files)
                {
                    stoppingToken.ThrowIfCancellationRequested();

                    try
                    {
                        await minioService.DeleteFileAsync(file.Token);
                        await fileRepository.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cleanup unattached file {FileId}", file.Id);
                    }
                }

                if (files.Count > 0)
                    await fileRepository.SaveChangesAsync();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unattached file cleanup failed.");
            }
        }
    }
}
