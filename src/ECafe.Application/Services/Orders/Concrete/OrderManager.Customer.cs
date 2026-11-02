using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Order;
using ECafe.Application.Mappings;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Application.Services.Orders.Concrete;

public sealed partial class OrderManager
{
    /// <summary>Müştərinin öz açıq sessiyasına təsdiq gözləyən sifarişini masa kilidi və idempotency açarı ilə bir dəfə yaradır; stok və ödəniş etmir.</summary>
    public async Task<CreateCustomerOrderResult> CreateAsync(int tableSessionId, Guid requestId,
        CreateCustomerOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (GetCurrentRoleId() != (int)RoleCode.Customer) throw new ForbiddenException(ErrorCode.AccessDenied);
        if (request is null) throw new BadRequestException(ErrorCode.RequestCannotBeNull);
        if (requestId == Guid.Empty) throw new BadRequestException(ErrorCode.OrderRequestKeyRequired);
        if (tableSessionId <= 0) throw new BadRequestException(ErrorCode.InvalidTableSessionId);
        var creation = CustomerOrderRequestMapper.ToCreationRequest(request);
        await _validator.ValidateAndThrowAsync(creation, cancellationToken);
        var normalized = NormalizeRequest(creation);
        var customerId = GetCurrentUserId();
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            TableSessionId = tableSessionId, CustomerUserId = customerId, Source = OrderSource.Customer, normalized.Note,
            Items = normalized.Items.Select(line => new
            {
                line.ItemId, line.Quantity, line.Note,
                ExpectedUnitPrice = line.ExpectedUnitPrice?.ToString("G29", CultureInfo.InvariantCulture)
            })
        })));
        var snapshot = await RequireCustomerOrderingSessionAsync(tableSessionId, customerId, cancellationToken);
        await using var transaction = await _transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await _tables.AcquireReservationLockAsync(snapshot.RestaurantId, snapshot.TableId, cancellationToken);
        var session = await RequireCustomerOrderingSessionAsync(tableSessionId, customerId, cancellationToken);
        if (session.TableId != snapshot.TableId || session.RestaurantId != snapshot.RestaurantId)
            throw new BusinessRuleException(ErrorCode.OrderSessionChanged);

        var existing = await _orders.FindCreationRequestAsync(session.RestaurantId, tableSessionId, requestId, cancellationToken);
        if (existing is not null)
        {
            if (existing.IsDeleted || existing.SourceId != (int)OrderSource.Customer ||
                existing.CustomerUserId != customerId || existing.CreationRequestHash != hash)
                throw new BusinessRuleException(ErrorCode.OrderRequestKeyConflict);
            await transaction.CommitAsync(cancellationToken);
            return new(CustomerOrderTrackingMapper.Map(existing) with
                { CanCancel = await CanCancelAsync(existing.Id, cancellationToken) }, true);
        }

        var lines = await BuildLinesAsync(session.RestaurantId, normalized.Items, cancellationToken);
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var order = new Domain.Entities.Order
        {
            RestaurantId = session.RestaurantId, TableSessionId = session.Id, TableId = session.TableId,
            ReservationId = session.ReservationId, CustomerUserId = customerId, WaiterUserId = session.WaiterUserId,
            SourceId = (int)OrderSource.Customer,
            StatusId = StatusIds.Build(StatusType.Order, (int)OrderStatus.PendingWaiterConfirmation),
            OpenedAt = new DateTime(nowUtc.Ticks - nowUtc.Ticks % TimeSpan.TicksPerMicrosecond, DateTimeKind.Utc), Note = normalized.Note,
            CreationRequestId = requestId, CreationRequestHash = hash, OrderItems = lines
        };
        await _orders.Add(order);
        await _orders.SaveChangesAsync();
        await _audit.RecordRestaurantActionAsync(order.RestaurantId, AuditActions.OrderCreated,
            new { orderId = order.Id, tableSessionId, tableId = order.TableId, sourceId = order.SourceId,
                awaitingWaiterConfirmation = true, lineCount = lines.Count }, AuditEntityTypes.Order, order.Id);
        await transaction.CommitAsync(cancellationToken);
        return new(CustomerOrderTrackingMapper.Map(order) with
            { CanCancel = await CanCancelAsync(order.Id, cancellationToken) }, false);
    }

    /// <summary>Giriş etmiş aktiv müştərinin aidiyyətini və sessiyanın sifariş qəbulunu kiliddən əvvəl və sonra eyni SQL qaydası ilə yoxlayır.</summary>
    private async Task<Domain.Entities.TableSession> RequireCustomerOrderingSessionAsync(int sessionId, int customerId,
        CancellationToken cancellationToken)
        => await _sessions.Query(session => session.Id == sessionId && session.CustomerUserId == customerId &&
                (!session.ReservationId.HasValue || session.Reservation != null && !session.Reservation.IsDeleted &&
                    session.Reservation.CustomerUserId == customerId && session.Reservation.RestaurantId == session.RestaurantId))
            .Where(CustomerOrderSubmissionPolicy.AvailableAt(_clock.GetUtcNow().UtcDateTime))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(ErrorCode.TableSessionNotFound);
}
