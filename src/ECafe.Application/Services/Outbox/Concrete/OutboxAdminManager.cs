using System.Text.Json;
using ECafe.Application.Common.Dates;
using ECafe.Application.Common.Outbox;
using ECafe.Application.Common.Pagination;
using ECafe.Application.DTOs.Outbox;
using ECafe.Application.Repository;
using ECafe.Application.Services.Outbox.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Outbox.Concrete
{
    public class OutboxAdminManager : IOutboxAdminService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly IBaseRepository<Domain.Entities.OutboxEvent> _outboxRepository;
        private readonly int _maxRetryCount;

        public OutboxAdminManager(
            IBaseRepository<Domain.Entities.OutboxEvent> outboxRepository,
            IConfiguration configuration)
        {
            _outboxRepository = outboxRepository;
            _maxRetryCount = GetPositiveIntSetting(configuration, "EmailOutbox:MaxRetryCount", 5);
        }

        public async Task<PaginatedList<OutboxMessageResponse>> GetMessagesAsync(OutboxMessageFilterRequest filter)
        {
            filter ??= new OutboxMessageFilterRequest();
            filter.PageNumber = PaginationFilterNormalizer.NormalizePageNumber(filter.PageNumber);
            filter.PageSize = PaginationFilterNormalizer.NormalizePageSize(filter.PageSize, defaultPageSize: 20);

            var now = DateTime.UtcNow;
            var query = _outboxRepository.Query(x => x.EventType == OutboxEventTypes.EmailNotificationRequested);

            if (filter.ChannelId.HasValue && filter.ChannelId.Value != (int)OutboxMessageChannel.Email)
                query = query.Where(_ => false);

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim().ToLowerInvariant();
                query = query.Where(x =>
                    x.AggregateType.ToLower().Contains(search) ||
                    x.Payload.ToLower().Contains(search));
            }

            if (filter.DateFrom.HasValue)
            {
                var dateFromUtc = DateTimeRangeNormalizer.ToUtcRangeStart(filter.DateFrom.Value);
                query = query.Where(x => x.OccurredAt >= dateFromUtc);
            }

            if (filter.DateTo.HasValue)
            {
                var dateToUtc = DateTimeRangeNormalizer.ToUtcRangeEnd(filter.DateTo.Value);
                query = query.Where(x => x.OccurredAt <= dateToUtc);
            }

            if (filter.StatusId.HasValue)
            {
                var status = (OutboxMessageStatus)filter.StatusId.Value;
                query = ApplyStatusFilter(query, status, now);
            }

            var page = await PaginatedList<Domain.Entities.OutboxEvent>.CreateAsync(
                query.OrderByDescending(x => x.OccurredAt),
                filter.PageNumber,
                filter.PageSize);

            var items = page.Items.Select(item => Map(item, now)).ToList();
            return new PaginatedList<OutboxMessageResponse>(items, page.TotalCount, page.PageIndex, filter.PageSize);
        }

        public async Task<OutboxMessageResponse> GetMessageAsync(Guid id)
        {
            var outboxEvent = await GetEmailOutboxEventAsync(id, tracked: false);
            return Map(outboxEvent, DateTime.UtcNow);
        }

        public async Task<OutboxMessageResponse> RetryAsync(Guid id)
        {
            var outboxEvent = await GetEmailOutboxEventAsync(id, tracked: true);
            var now = DateTime.UtcNow;
            var status = GetStatus(outboxEvent, now);

            if (status == OutboxMessageStatus.Sent)
                throw new BusinessRuleException(ErrorCode.OutboxMessageAlreadySent);

            if (status == OutboxMessageStatus.Processing)
                throw new BusinessRuleException(ErrorCode.OutboxMessageRetryNotAllowed);

            if (status == OutboxMessageStatus.Pending && string.IsNullOrWhiteSpace(outboxEvent.LastError))
                throw new BusinessRuleException(ErrorCode.OutboxMessageRetryNotAllowed);

            outboxEvent.LockedUntil = null;
            outboxEvent.LastError = null;
            outboxEvent.RetryCount = Math.Min(outboxEvent.RetryCount, Math.Max(_maxRetryCount - 1, 0));

            await _outboxRepository.SaveChangesAsync();
            return Map(outboxEvent, now);
        }

        private async Task<Domain.Entities.OutboxEvent> GetEmailOutboxEventAsync(Guid id, bool tracked)
        {
            var query = tracked ? _outboxRepository.QueryTracked() : _outboxRepository.Query();
            var outboxEvent = await query.FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.EventType == OutboxEventTypes.EmailNotificationRequested);

            return outboxEvent ?? throw new ECafe.Application.Common.Exceptions.NotFoundException(ErrorCode.OutboxMessageNotFound);
        }

        private IQueryable<Domain.Entities.OutboxEvent> ApplyStatusFilter(
            IQueryable<Domain.Entities.OutboxEvent> query,
            OutboxMessageStatus status,
            DateTime now)
            => status switch
            {
                OutboxMessageStatus.Sent => query.Where(x => x.ProcessedAt != null),
                OutboxMessageStatus.Processing => query.Where(x => x.ProcessedAt == null && x.LockedUntil != null && x.LockedUntil > now),
                OutboxMessageStatus.Failed => query.Where(x => x.ProcessedAt == null && x.RetryCount >= _maxRetryCount),
                OutboxMessageStatus.Pending => query.Where(x =>
                    x.ProcessedAt == null &&
                    x.RetryCount < _maxRetryCount &&
                    (x.LockedUntil == null || x.LockedUntil <= now)),
                _ => query
            };

        private OutboxMessageResponse Map(Domain.Entities.OutboxEvent outboxEvent, DateTime now)
        {
            var payload = DeserializePayload(outboxEvent.Payload);
            var status = GetStatus(outboxEvent, now);

            return new OutboxMessageResponse
            {
                Id = outboxEvent.Id,
                EventType = outboxEvent.EventType,
                AggregateType = outboxEvent.AggregateType,
                AggregateId = outboxEvent.AggregateId,
                ChannelId = (int)OutboxMessageChannel.Email,
                Channel = "Email",
                StatusId = (int)status,
                Status = GetStatusName(status),
                Recipient = payload?.ToEmail ?? "-",
                RecipientName = payload?.ToName ?? "-",
                Subject = payload?.Subject ?? outboxEvent.EventType,
                RetryCount = outboxEvent.RetryCount,
                MaxRetryCount = _maxRetryCount,
                OccurredAt = outboxEvent.OccurredAt,
                ProcessedAt = outboxEvent.ProcessedAt,
                LockedUntil = outboxEvent.LockedUntil,
                NextRetryAt = GetNextRetryAt(outboxEvent, status),
                LastError = outboxEvent.LastError,
                RelatedEntityType = payload?.RelatedEntityType,
                RelatedEntityId = payload?.RelatedEntityId
            };
        }

        private static EmailNotificationOutboxPayload? DeserializePayload(string payload)
        {
            try
            {
                return JsonSerializer.Deserialize<EmailNotificationOutboxPayload>(payload, JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private OutboxMessageStatus GetStatus(Domain.Entities.OutboxEvent outboxEvent, DateTime now)
        {
            if (outboxEvent.ProcessedAt.HasValue)
                return OutboxMessageStatus.Sent;

            if (outboxEvent.RetryCount >= _maxRetryCount)
                return OutboxMessageStatus.Failed;

            if (outboxEvent.LockedUntil.HasValue && outboxEvent.LockedUntil.Value > now)
                return OutboxMessageStatus.Processing;

            return OutboxMessageStatus.Pending;
        }

        private static DateTime? GetNextRetryAt(Domain.Entities.OutboxEvent outboxEvent, OutboxMessageStatus status)
            => status is OutboxMessageStatus.Pending or OutboxMessageStatus.Processing
                ? outboxEvent.LockedUntil
                : null;

        private static string GetStatusName(OutboxMessageStatus status)
            => status switch
            {
                OutboxMessageStatus.Pending => "Gözləyir",
                OutboxMessageStatus.Processing => "İcra olunur",
                OutboxMessageStatus.Sent => "Göndərildi",
                OutboxMessageStatus.Failed => "Uğursuz",
                _ => status.ToString()
            };

        private static int GetPositiveIntSetting(IConfiguration configuration, string key, int fallback)
        {
            var value = configuration[key];
            return int.TryParse(value, out var parsed) && parsed > 0
                ? parsed
                : fallback;
        }
    }
}
