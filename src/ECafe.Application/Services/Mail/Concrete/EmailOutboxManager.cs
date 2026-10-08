using ECafe.Application.Common.Outbox;
using ECafe.Application.Repository;
using ECafe.Application.Services.Monitoring.Abstract;
using ECafe.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace ECafe.Application.Services
{
    public class EmailOutboxManager : IEmailOutboxService, IEmailOutboxProcessor
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private static readonly int[] DefaultRetryDelaySeconds = [30, 120, 300, 900, 1800];

        private readonly IBaseRepository<Domain.Entities.OutboxEvent> _outboxRepository;
        private readonly IEmailService _emailService;
        private readonly ICriticalEventReporter _criticalEventReporter;
        private readonly TimeSpan _lockDuration;
        private readonly int _maxRetryCount;

        public EmailOutboxManager(
            IBaseRepository<Domain.Entities.OutboxEvent> outboxRepository,
            IEmailService emailService,
            ICriticalEventReporter criticalEventReporter,
            IConfiguration configuration)
        {
            _outboxRepository = outboxRepository;
            _emailService = emailService;
            _criticalEventReporter = criticalEventReporter;
            _lockDuration = TimeSpan.FromSeconds(GetPositiveIntSetting(configuration, "EmailOutbox:LockSeconds", 300));
            _maxRetryCount = GetPositiveIntSetting(configuration, "EmailOutbox:MaxRetryCount", 5);
        }

        public async Task EnqueueEmailAsync(
            string toEmail,
            string toName,
            string subject,
            string body,
            string aggregateType,
            long aggregateId,
            string? relatedEntityType = null,
            long? relatedEntityId = null)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                throw new BusinessRuleException(ErrorCode.EmailRecipientRequired);

            if (string.IsNullOrWhiteSpace(subject))
                throw new BusinessRuleException(ErrorCode.EmailSubjectRequired);

            if (string.IsNullOrWhiteSpace(body))
                throw new BusinessRuleException(ErrorCode.EmailBodyRequired);

            if (string.IsNullOrWhiteSpace(aggregateType))
                throw new BusinessRuleException(ErrorCode.EmailAggregateTypeRequired);

            if (aggregateId <= 0)
                throw new BusinessRuleException(ErrorCode.InvalidEmailAggregateId);

            var normalizedAggregateType = aggregateType.Trim();
            var normalizedToName = string.IsNullOrWhiteSpace(toName) ? "Istifadeci" : toName.Trim();
            var payload = new EmailNotificationOutboxPayload
            {
                ToEmail = toEmail.Trim(),
                ToName = normalizedToName,
                Subject = subject.Trim(),
                Body = body.Trim(),
                RelatedEntityType = string.IsNullOrWhiteSpace(relatedEntityType)
                    ? normalizedAggregateType
                    : relatedEntityType.Trim(),
                RelatedEntityId = relatedEntityId ?? aggregateId
            };

            var outboxEvent = new Domain.Entities.OutboxEvent
            {
                Id = Guid.NewGuid(),
                EventType = OutboxEventTypes.EmailNotificationRequested,
                AggregateType = normalizedAggregateType,
                AggregateId = aggregateId,
                Payload = JsonSerializer.Serialize(payload, JsonOptions),
                OccurredAt = DateTime.UtcNow
            };

            await _outboxRepository.Add(outboxEvent);
            await _outboxRepository.SaveChangesAsync();
        }

        public async Task<int> ProcessPendingAsync(int batchSize, CancellationToken cancellationToken = default)
        {
            if (batchSize <= 0)
                batchSize = 50;

            var now = DateTime.UtcNow;
            var outboxEvents = await _outboxRepository.QueryTracked(x =>
                    x.EventType == OutboxEventTypes.EmailNotificationRequested &&
                    x.ProcessedAt == null &&
                    x.RetryCount < _maxRetryCount &&
                    (x.LockedUntil == null || x.LockedUntil <= now))
                .OrderBy(x => x.OccurredAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            foreach (var outboxEvent in outboxEvents)
            {
                outboxEvent.LockedUntil = now.Add(_lockDuration);
            }

            if (outboxEvents.Count > 0)
                await _outboxRepository.SaveChangesAsync();

            var processedCount = 0;
            foreach (var outboxEvent in outboxEvents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await ProcessOutboxEventAsync(outboxEvent);
                    outboxEvent.ProcessedAt = DateTime.UtcNow;
                    outboxEvent.LockedUntil = null;
                    outboxEvent.LastError = null;
                    processedCount++;
                }
                catch (Exception ex)
                {
                    outboxEvent.RetryCount++;
                    var retryLimitReached = outboxEvent.RetryCount >= _maxRetryCount;
                    outboxEvent.LockedUntil = outboxEvent.RetryCount >= _maxRetryCount
                        ? null
                        : DateTime.UtcNow.Add(GetRetryDelay(outboxEvent.RetryCount));
                    outboxEvent.LastError = ex.Message.Length > 2000
                        ? ex.Message[..2000]
                        : ex.Message;

                    if (retryLimitReached)
                        await ReportOutboxRetryLimitReachedAsync(outboxEvent, ex);
                }

                await _outboxRepository.SaveChangesAsync();
            }

            return processedCount;
        }

        // Növbədəki e-poçtu göndərib nəticəsinə görə hadisənin vəziyyətini yeniləyir.
        private async Task ProcessOutboxEventAsync(Domain.Entities.OutboxEvent outboxEvent)
        {
            if (outboxEvent.EventType == OutboxEventTypes.EmailNotificationRequested)
            {
                var payload = JsonSerializer.Deserialize<EmailNotificationOutboxPayload>(
                    outboxEvent.Payload,
                    JsonOptions);

                if (payload is null)
                    throw new BusinessRuleException(ErrorCode.EmailOutboxPayloadInvalid);

                await _emailService.SendContractNotificationAsync(
                    payload.ToEmail,
                    payload.ToName,
                    payload.Subject,
                    payload.Body);

                return;
            }

            throw new BusinessRuleException(ErrorCode.UnsupportedOutboxEventType, new { eventType = outboxEvent.EventType });
        }

        // Uğursuz göndərişlər arasında artan gözləmə müddətini hesablayır.
        private static TimeSpan GetRetryDelay(int retryCount)
        {
            var delayIndex = Math.Clamp(retryCount - 1, 0, DefaultRetryDelaySeconds.Length - 1);
            return TimeSpan.FromSeconds(DefaultRetryDelaySeconds[delayIndex]);
        }

        // Təkrar cəhd limiti bitəndə problemi qeyd edir.
        private Task ReportOutboxRetryLimitReachedAsync(Domain.Entities.OutboxEvent outboxEvent, Exception exception)
            => _criticalEventReporter.CaptureAsync(new CriticalEvent(
                Category: "notification",
                Name: "outbox_retry_limit_reached",
                Severity: CriticalEventSeverity.Error,
                Properties: new Dictionary<string, string?>
                {
                    ["outboxEventId"] = outboxEvent.Id.ToString(),
                    ["eventType"] = outboxEvent.EventType,
                    ["aggregateType"] = outboxEvent.AggregateType,
                    ["retryCount"] = outboxEvent.RetryCount.ToString(),
                    ["maxRetryCount"] = _maxRetryCount.ToString(),
                    ["exceptionType"] = exception.GetType().Name
                }));

        // Növbə limitini konfiqurasiyadan müsbət ədəd kimi oxuyur.
        private static int GetPositiveIntSetting(IConfiguration configuration, string key, int fallback)
        {
            var value = configuration[key];
            return int.TryParse(value, out var parsed) && parsed > 0
                ? parsed
                : fallback;
        }
    }
}
