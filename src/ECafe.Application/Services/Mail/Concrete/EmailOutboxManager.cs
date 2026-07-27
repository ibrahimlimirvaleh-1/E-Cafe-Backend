using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Outbox;
using ECafe.Application.Repository;
using ECafe.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ECafe.Application.Services
{
    public class EmailOutboxManager : IEmailOutboxService, IEmailOutboxProcessor
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(5);
        private const int MaxRetryCount = 5;

        private readonly IBaseRepository<Domain.Entities.OutboxEvent> _outboxRepository;
        private readonly IEmailService _emailService;

        public EmailOutboxManager(
            IBaseRepository<Domain.Entities.OutboxEvent> outboxRepository,
            IEmailService emailService)
        {
            _outboxRepository = outboxRepository;
            _emailService = emailService;
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

            var payload = new EmailNotificationOutboxPayload
            {
                ToEmail = toEmail.Trim(),
                ToName = string.IsNullOrWhiteSpace(toName) ? "İstifadəçi" : toName.Trim(),
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

        public async Task<int> ProcessPendingAsync(int batchSize, CancellationToken cancellationToken = default)
        {
            if (batchSize <= 0)
                batchSize = 50;

            var now = DateTime.UtcNow;
            var outboxEvents = await _outboxRepository.QueryTracked(x =>
                    x.EventType == OutboxEventTypes.EmailNotificationRequested &&
                    x.ProcessedAt == null &&
                    x.RetryCount < MaxRetryCount &&
                    (x.LockedUntil == null || x.LockedUntil <= now))
                .OrderBy(x => x.OccurredAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            foreach (var outboxEvent in outboxEvents)
            {
                outboxEvent.LockedUntil = now.Add(LockDuration);
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
                    outboxEvent.LockedUntil = null;
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
            if (outboxEvent.EventType != OutboxEventTypes.EmailNotificationRequested)
                throw new BusinessRuleException($"Unsupported outbox event type: {outboxEvent.EventType}");

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
        }
    }
}
