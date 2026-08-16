using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Outbox;
using ECafe.Application.Common.Pagination;
using ECafe.Application.DTOs.AuditLog;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Domain.Exceptions;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using System.Text.Json;

namespace ECafe.Application.Services.AuditLog.Concrete
{
    public class AuditLogManager : BaseManager, IAuditLogService, IAuditOutboxProcessor
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(5);
        private const int MaxRetryCount = 5;

        private readonly IBaseRepository<Domain.Entities.AuditLog> _auditLogRepository;
        private readonly IBaseRepository<Domain.Entities.OutboxEvent> _outboxRepository;

        public AuditLogManager(
            IHttpContextAccessor httpContextAccessor,
            IMapper mapper,
            IConfiguration configuration,
            IBaseRepository<Domain.Entities.AuditLog> auditLogRepository,
            IBaseRepository<Domain.Entities.OutboxEvent> outboxRepository)
            : base(httpContextAccessor, mapper, configuration)
        {
            _auditLogRepository = auditLogRepository;
            _outboxRepository = outboxRepository;
        }

        public async Task RecordRestaurantActionAsync(
            int restaurantId,
            string action,
            object? metadata = null,
            string entityType = AuditEntityTypes.Restaurant,
            long? entityId = null,
            string? entityDisplayName = null)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant ID!");

            if (string.IsNullOrWhiteSpace(action))
                throw new BusinessRuleException("Audit action is required.");

            if (string.IsNullOrWhiteSpace(entityType))
                throw new BusinessRuleException("Audit entity type is required.");

            var now = DateTime.UtcNow;
            var payload = new AuditOutboxPayload
            {
                RestaurantId = restaurantId,
                ActorUserId = TryGetCurrentUserId(),
                ActorFullName = GetActorFullName(),
                ActorRoleId = TryGetCurrentRoleId(),
                ActorRoleName = GetClaimValue("roleName"),
                ActorEmail = GetClaimValue(ClaimTypes.Email) ?? GetClaimValue("email"),
                EntityType = entityType.Trim(),
                EntityId = entityId ?? restaurantId,
                EntityDisplayName = string.IsNullOrWhiteSpace(entityDisplayName)
                    ? null
                    : entityDisplayName.Trim(),
                Action = action.Trim(),
                Metadata = metadata is null ? null : JsonSerializer.Serialize(metadata, JsonOptions),
                CorrelationId = GetCorrelationId(),
                IpAddress = GetClientIpAddress(),
                UserAgent = GetUserAgent(),
                OccurredAt = now
            };

            var outboxEvent = new Domain.Entities.OutboxEvent
            {
                Id = Guid.NewGuid(),
                EventType = OutboxEventTypes.AuditLogRequested,
                AggregateType = OutboxAggregateTypes.Restaurant,
                AggregateId = restaurantId,
                Payload = JsonSerializer.Serialize(payload, JsonOptions),
                OccurredAt = now
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
                    x.EventType == OutboxEventTypes.AuditLogRequested &&
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

        public Task<PaginatedList<AuditLogResponse>> GetRestaurantTimelineAsync(
            int restaurantId,
            AuditLogFilterRequest filter)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant ID!");

            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            filter ??= new AuditLogFilterRequest();
            filter.PageNumber = PaginationFilterNormalizer.NormalizePageNumber(filter.PageNumber);
            filter.PageSize = PaginationFilterNormalizer.NormalizePageSize(filter.PageSize, defaultPageSize: 20);

            var query = _auditLogRepository.Query(x =>
                x.RestaurantId == restaurantId ||
                (x.RestaurantId == null && x.EntityName == AuditEntityTypes.Restaurant && x.EntityId == restaurantId));

            if (!string.IsNullOrWhiteSpace(filter.Action))
            {
                var action = filter.Action.Trim();
                query = query.Where(x => x.Action == action);
            }

            if (filter.DateFrom.HasValue)
            {
                var dateFromUtc = NormalizeUtc(filter.DateFrom.Value);
                query = query.Where(x => (x.OccurredAt ?? x.CreatedAt) >= dateFromUtc);
            }

            if (filter.DateTo.HasValue)
            {
                var dateToUtc = NormalizeUtcDateTo(filter.DateTo.Value);
                query = query.Where(x => (x.OccurredAt ?? x.CreatedAt) <= dateToUtc);
            }

            var response = query
                .OrderByDescending(x => x.OccurredAt ?? x.CreatedAt)
                .Select(x => new AuditLogResponse
                {
                    Id = x.Id,
                    EventId = x.EventId,
                    RestaurantId = x.RestaurantId,
                    UserId = x.UserId,
                    ActorUserId = x.UserId,
                    ActorFullName = x.ActorFullName,
                    UserName = x.ActorFullName,
                    ActorRoleId = x.ActorRoleId,
                    ActorRoleName = x.ActorRoleName,
                    RoleName = x.ActorRoleName,
                    ActorEmail = x.ActorEmail,
                    EntityName = x.EntityName,
                    EntityId = x.EntityId,
                    EntityDisplayName = x.EntityDisplayName,
                    Action = x.Action,
                    ActionDisplayName = AuditActions.GetDisplayName(x.Action),
                    NewValues = x.NewValues,
                    Metadata = x.Metadata,
                    CorrelationId = x.CorrelationId,
                    IpAddress = x.IpAddress,
                    UserAgent = x.UserAgent,
                    OccurredAt = x.OccurredAt,
                    CreatedAt = x.CreatedAt
                });

            return PaginatedList<AuditLogResponse>.CreateAsync(
                response,
                filter.PageNumber,
                filter.PageSize);
        }

        private async Task ProcessOutboxEventAsync(Domain.Entities.OutboxEvent outboxEvent)
        {
            if (outboxEvent.EventType != OutboxEventTypes.AuditLogRequested)
                throw new BusinessRuleException($"Unsupported outbox event type: {outboxEvent.EventType}");

            var exists = await _auditLogRepository.CheckExistAsync(x => x.EventId == outboxEvent.Id);
            if (exists)
                return;

            var payload = JsonSerializer.Deserialize<AuditOutboxPayload>(outboxEvent.Payload, JsonOptions);
            if (payload is null)
                throw new BusinessRuleException("Audit outbox payload is invalid.");

            var auditLog = new Domain.Entities.AuditLog
            {
                EventId = outboxEvent.Id,
                RestaurantId = payload.RestaurantId,
                UserId = payload.ActorUserId,
                ActorFullName = payload.ActorFullName,
                ActorRoleId = payload.ActorRoleId,
                ActorRoleName = payload.ActorRoleName,
                ActorEmail = payload.ActorEmail,
                EntityName = payload.EntityType,
                EntityId = payload.EntityId,
                EntityDisplayName = payload.EntityDisplayName,
                Action = payload.Action,
                NewValues = payload.Metadata,
                Metadata = payload.Metadata,
                CorrelationId = payload.CorrelationId,
                IpAddress = payload.IpAddress,
                UserAgent = payload.UserAgent,
                OccurredAt = payload.OccurredAt
            };

            await _auditLogRepository.Add(auditLog);
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
                return value;

            if (value.Kind == DateTimeKind.Local)
                return value.ToUniversalTime();

            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static DateTime NormalizeUtcDateTo(DateTime value)
        {
            var utc = NormalizeUtc(value);
            return utc.TimeOfDay == TimeSpan.Zero
                ? utc.Date.AddDays(1).AddTicks(-1)
                : utc;
        }

        private int? TryGetCurrentUserId()
        {
            var userIdClaim = HttpContextAccessor.HttpContext?.User.FindFirst("userId")?.Value;
            return int.TryParse(userIdClaim, out var userId) && userId > 0
                ? userId
                : null;
        }

        private int? TryGetCurrentRoleId()
        {
            var roleClaim = GetClaimValue(ClaimTypes.Role);
            return int.TryParse(roleClaim, out var roleId) && roleId > 0
                ? roleId
                : null;
        }

        private string? GetActorFullName()
        {
            var name = GetClaimValue(ClaimTypes.Name) ?? GetClaimValue("name");
            var surname = GetClaimValue(ClaimTypes.Surname) ?? GetClaimValue("surname");
            var fullName = string.Join(' ', new[] { name, surname }.Where(x => !string.IsNullOrWhiteSpace(x)));
            return string.IsNullOrWhiteSpace(fullName) ? null : fullName.Trim();
        }

        private string? GetClaimValue(string claimType)
        {
            var value = HttpContextAccessor.HttpContext?.User.FindFirst(claimType)?.Value;
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private string? GetClientIpAddress()
        {
            var context = HttpContextAccessor.HttpContext;
            if (context is null)
                return null;

            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
                return forwardedFor.Split(',')[0].Trim();

            return context.Connection.RemoteIpAddress?.ToString();
        }

        private string? GetUserAgent()
            => HttpContextAccessor.HttpContext?.Request.Headers["User-Agent"].FirstOrDefault();

        private string? GetCorrelationId()
        {
            var context = HttpContextAccessor.HttpContext;
            if (context is null)
                return null;

            var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(correlationId)
                ? context.TraceIdentifier
                : correlationId.Trim();
        }
    }
}
