using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Outbox;
using ECafe.Application.Repository;
using ECafe.Application.Services.Sms.Abstract;
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
        private readonly ISmsService _smsService;
        private readonly TimeSpan _lockDuration;
        private readonly int _maxRetryCount;

        public EmailOutboxManager(
            IBaseRepository<Domain.Entities.OutboxEvent> outboxRepository,
            IEmailService emailService,
            ISmsService smsService,
            IConfiguration configuration)
        {
            _outboxRepository = outboxRepository;
            _emailService = emailService;
            _smsService = smsService;
            _lockDuration = TimeSpan.FromSeconds(GetPositiveIntSetting(configuration, "EmailOutbox:LockSeconds", 300));
            _maxRetryCount = GetPositiveIntSetting(configuration, "EmailOutbox:MaxRetryCount", 5);
        }

        public async Task EnqueueContractNotificationAsync(
            string toEmail,
            string toName,
            string subject,
            string body,
            long contractId = 0)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                throw new BusinessRuleException("Email recipient is required.");

            if (string.IsNullOrWhiteSpace(subject))
                throw new BusinessRuleException("Email subject is required.");

            if (string.IsNullOrWhiteSpace(body))
                throw new BusinessRuleException("Email body is required.");

            if (contractId <= 0)
                throw new BusinessRuleException("Invalid contract ID.");

            var normalizedToName = string.IsNullOrWhiteSpace(toName) ? "İstifadəçi" : toName.Trim();
            var payload = new EmailNotificationOutboxPayload
            {
                ToEmail = toEmail.Trim(),
                ToName = normalizedToName,
                Subject = subject.Trim(),
                Body = body.Trim(),
                RelatedEntityType = AuditEntityTypes.Contract,
                RelatedEntityId = contractId
            };

            var outboxEvent = new Domain.Entities.OutboxEvent
            {
                Id = Guid.NewGuid(),
                EventType = OutboxEventTypes.EmailNotificationRequested,
                AggregateType = OutboxAggregateTypes.Contract,
                AggregateId = contractId,
                Payload = JsonSerializer.Serialize(payload, JsonOptions),
                OccurredAt = DateTime.UtcNow
            };

            await _outboxRepository.Add(outboxEvent);
            await _outboxRepository.SaveChangesAsync();
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
                throw new BusinessRuleException("Email recipient is required.");

            if (string.IsNullOrWhiteSpace(subject))
                throw new BusinessRuleException("Email subject is required.");

            if (string.IsNullOrWhiteSpace(body))
                throw new BusinessRuleException("Email body is required.");

            if (string.IsNullOrWhiteSpace(aggregateType))
                throw new BusinessRuleException("Email aggregate type is required.");

            if (aggregateId <= 0)
                throw new BusinessRuleException("Invalid email aggregate ID.");

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

        public async Task EnqueueSmsAsync(
            string toPhone,
            string toName,
            string subject,
            string body,
            string aggregateType,
            long aggregateId,
            string? relatedEntityType = null,
            long? relatedEntityId = null)
        {
            if (string.IsNullOrWhiteSpace(toPhone))
                throw new BusinessRuleException("SMS recipient phone is required.");

            if (string.IsNullOrWhiteSpace(subject))
                throw new BusinessRuleException("SMS subject is required.");

            if (string.IsNullOrWhiteSpace(body))
                throw new BusinessRuleException("SMS body is required.");

            if (string.IsNullOrWhiteSpace(aggregateType))
                throw new BusinessRuleException("SMS aggregate type is required.");

            if (aggregateId <= 0)
                throw new BusinessRuleException("Invalid SMS aggregate ID.");

            var normalizedAggregateType = aggregateType.Trim();
            var normalizedToName = string.IsNullOrWhiteSpace(toName) ? "Istifadeci" : toName.Trim();
            var payload = new SmsNotificationOutboxPayload
            {
                ToPhone = toPhone.Trim(),
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
                EventType = OutboxEventTypes.SmsNotificationRequested,
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
                    (x.EventType == OutboxEventTypes.EmailNotificationRequested ||
                     x.EventType == OutboxEventTypes.SmsNotificationRequested) &&
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
                    outboxEvent.LockedUntil = outboxEvent.RetryCount >= _maxRetryCount
                        ? null
                        : DateTime.UtcNow.Add(GetRetryDelay(outboxEvent.RetryCount));
                    outboxEvent.LastError = ex.Message.Length > 2000
                        ? ex.Message[..2000]
                        : ex.Message;
                }

                await _outboxRepository.SaveChangesAsync();
            }

            return processedCount;
        }

        private async Task ProcessOutboxEventAsync(Domain.Entities.OutboxEvent outboxEvent)
        {
            if (outboxEvent.EventType == OutboxEventTypes.EmailNotificationRequested)
            {
                var payload = JsonSerializer.Deserialize<EmailNotificationOutboxPayload>(
                    outboxEvent.Payload,
                    JsonOptions);

                if (payload is null)
                    throw new BusinessRuleException("Email outbox payload is invalid.");

                await _emailService.SendContractNotificationAsync(
                    payload.ToEmail,
                    payload.ToName,
                    payload.Subject,
                    payload.Body);

                return;
            }

            if (outboxEvent.EventType == OutboxEventTypes.SmsNotificationRequested)
            {
                var payload = JsonSerializer.Deserialize<SmsNotificationOutboxPayload>(
                    outboxEvent.Payload,
                    JsonOptions);

                if (payload is null)
                    throw new BusinessRuleException("SMS outbox payload is invalid.");

                await _smsService.SendAsync(
                    payload.ToPhone,
                    BuildSmsContent(payload.Subject, payload.Body),
                    outboxEvent.Id.ToString());

                return;
            }

            throw new BusinessRuleException($"Unsupported outbox event type: {outboxEvent.EventType}");
        }

        private static string BuildSmsContent(string subject, string body)
        {
            var content = $"{subject}: {body}".ReplaceLineEndings(" ").Trim();
            return content.Length <= 320 ? content : $"{content[..317]}...";
        }

        private static TimeSpan GetRetryDelay(int retryCount)
        {
            var delayIndex = Math.Clamp(retryCount - 1, 0, DefaultRetryDelaySeconds.Length - 1);
            return TimeSpan.FromSeconds(DefaultRetryDelaySeconds[delayIndex]);
        }

        private static int GetPositiveIntSetting(IConfiguration configuration, string key, int fallback)
        {
            var value = configuration[key];
            return int.TryParse(value, out var parsed) && parsed > 0
                ? parsed
                : fallback;
        }
    }
}
