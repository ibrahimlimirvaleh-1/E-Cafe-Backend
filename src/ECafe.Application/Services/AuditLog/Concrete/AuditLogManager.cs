using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Dates;
using ECafe.Application.Common.Outbox;
using ECafe.Application.Common.Pagination;
using ECafe.Application.DTOs.AuditLog;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Domain.Exceptions;
using ECafe.Shared.DTOs;
using ECafe.Shared.Extensions;
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

        public async Task<PaginatedList<AuditLogResponse>> GetRestaurantTimelineAsync(
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
                var dateFromUtc = DateTimeRangeNormalizer.ToUtcRangeStart(filter.DateFrom.Value);
                query = query.Where(x => (x.OccurredAt ?? x.CreatedAt) >= dateFromUtc);
            }

            if (filter.DateTo.HasValue)
            {
                var dateToUtc = DateTimeRangeNormalizer.ToUtcRangeEnd(filter.DateTo.Value);
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

            var paginatedResponse = await PaginatedList<AuditLogResponse>.CreateAsync(
                response,
                filter.PageNumber,
                filter.PageSize);

            foreach (var item in paginatedResponse.Items)
                item.Details = BuildDetails(item.Metadata);

            return paginatedResponse;
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

        private static List<AuditLogDetailResponse> BuildDetails(string? metadata)
        {
            if (string.IsNullOrWhiteSpace(metadata))
                return [];

            try
            {
                using var document = JsonDocument.Parse(metadata);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return [];

                var details = new List<AuditLogDetailResponse>();
                var scalarProperties = document.RootElement.EnumerateObject()
                    .Where(property => property.Value.ValueKind is not JsonValueKind.Object and not JsonValueKind.Array)
                    .ToDictionary(property => property.Name, property => property.Value, StringComparer.OrdinalIgnoreCase);

                if (document.RootElement.TryGetProperty("changedFields", out var changedFields) &&
                    changedFields.ValueKind == JsonValueKind.Array)
                {
                    foreach (var changedField in changedFields.EnumerateArray())
                    {
                        var field = ReadString(changedField, "field");
                        if (string.IsNullOrWhiteSpace(field))
                            continue;

                        if (ShouldSkipTechnicalField(field, scalarProperties))
                            continue;

                        details.Add(new AuditLogDetailResponse
                        {
                            Label = ToFriendlyLabel(field),
                            OldValue = ToFriendlyValue(field, ReadElementAsString(changedField, "oldValue")),
                            NewValue = ToFriendlyValue(field, ReadElementAsString(changedField, "newValue"))
                        });
                    }
                }

                foreach (var property in scalarProperties)
                {
                    if (property.Key.Equals("changedFields", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (ShouldSkipTechnicalField(property.Key, scalarProperties))
                        continue;

                    details.Add(new AuditLogDetailResponse
                    {
                        Label = ToFriendlyLabel(property.Key),
                        Value = ToFriendlyValue(property.Key, ElementToDisplayValue(property.Value))
                    });
                }

                return details;
            }
            catch (JsonException)
            {
                return [];
            }
        }

        private static string? ReadString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property))
                return null;

            return property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : ElementToDisplayValue(property);
        }

        private static string? ReadElementAsString(JsonElement element, string propertyName)
            => element.TryGetProperty(propertyName, out var property)
                ? ElementToDisplayValue(property)
                : null;

        private static string? ElementToDisplayValue(JsonElement element)
            => element.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.Undefined => null,
                JsonValueKind.String => element.GetString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => element.GetRawText()
            };

        private static bool ShouldSkipTechnicalField(
            string field,
            IReadOnlyDictionary<string, JsonElement> properties)
        {
            if (field.Equals("itemId", StringComparison.OrdinalIgnoreCase) ||
                field.Equals("fileId", StringComparison.OrdinalIgnoreCase))
                return true;

            if (field.Equals("categoryId", StringComparison.OrdinalIgnoreCase) &&
                properties.ContainsKey("categoryName"))
                return true;

            if (field.Equals("statusId", StringComparison.OrdinalIgnoreCase) &&
                properties.ContainsKey("statusName"))
                return true;

            return false;
        }

        private static string ToFriendlyLabel(string field)
            => field.Trim() switch
            {
                "name" or "Name" => "Məhsul adı",
                "categoryName" or "CategoryName" => "Kateqoriya",
                "categoryId" or "CategoryId" => "Kateqoriya",
                "statusName" or "StatusName" => "Status",
                "statusId" or "StatusId" => "Status",
                "basePrice" or "BasePrice" => "Qiymət",
                "sortOrder" or "SortOrder" => "Sıra",
                "slug" or "Slug" => "Slug",
                "commissionPercent" or "CommissionPercent" => "Komissiya faizi",
                "staffSettlementPeriod" or "StaffSettlementPeriod" => "Hesablaşma dövrü",
                "startDate" or "StartDate" => "Başlama tarixi",
                "endDate" or "EndDate" => "Bitmə tarixi",
                "paymentPolicyId" or "PaymentPolicyId" => "Ödəniş siyasəti",
                _ => SplitCamelCase(field)
            };

        private static string? ToFriendlyValue(string field, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            if (field.Equals("statusId", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(value, out var statusId))
                return ResolveKnownStatusName(statusId) ?? $"ID #{statusId}";

            if (field.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
                return $"ID #{value}";

            return value;
        }

        private static string? ResolveKnownStatusName(int statusId)
        {
            var statusTypeId = statusId / 1000;
            var statusValue = statusId % 1000;

            if (statusTypeId == (int)ECafe.Domain.Enums.StatusType.ItemStatus &&
                Enum.IsDefined(typeof(ECafe.Domain.Enums.ItemStatus), statusValue))
                return ((ECafe.Domain.Enums.ItemStatus)statusValue).GetDescription();

            return null;
        }

        private static string SplitCamelCase(string value)
            => string.Concat(value.Select((character, index) =>
                index > 0 && char.IsUpper(character) ? $" {character}" : character.ToString()));

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
