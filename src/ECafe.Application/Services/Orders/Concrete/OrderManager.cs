using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Common.Pagination;
using ECafe.Application.DTOs.Order;
using ECafe.Application.Mappings;
using ECafe.Application.Services.Orders.Abstract;
using ECafe.Application.Repositories.Item;
using ECafe.Application.Repositories.Order;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repositories.TableSession;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.TableSessions.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Shared.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ECafe.Application.Services.Orders.Concrete;

public sealed partial class OrderManager : BaseManager, IOrderService
{
    private const decimal MaxStoredAmount = 99999999.99m;
    private readonly IOrderRepository _orders;
    private readonly IItemRepository _items;
    private readonly ITableSessionRepository _sessions;
    private readonly ITableSessionService _sessionAccess;
    private readonly ITableRepository _tables;
    private readonly IApplicationDbTransactionFactory _transactions;
    private readonly IAuditLogService _audit;
    private readonly IValidator<CreateWaiterOrderRequest> _validator;
    private readonly OrderCreationOptions _limits;
    private readonly TimeProvider _clock;

    public OrderManager(IHttpContextAccessor accessor, IMapper mapper, IConfiguration configuration,
        IOrderRepository orders, IItemRepository items, ITableSessionRepository sessions,
        ITableSessionService sessionAccess, ITableRepository tables, IApplicationDbTransactionFactory transactions,
        IAuditLogService audit, IValidator<CreateWaiterOrderRequest> validator,
        IOptions<OrderCreationOptions> limits, TimeProvider clock) : base(accessor, mapper, configuration)
    {
        _orders = orders;
        _items = items;
        _sessions = sessions;
        _sessionAccess = sessionAccess;
        _tables = tables;
        _transactions = transactions;
        _audit = audit;
        _validator = validator;
        _limits = limits.Value;
        _clock = clock;
    }

    public async Task<CreateOrderResult> CreateForWaiterAsync(int restaurantId, int tableSessionId, Guid requestId,
        CreateWaiterOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new BadRequestException(ErrorCode.RequestCannotBeNull);
        if (requestId == Guid.Empty)
            throw new BadRequestException(ErrorCode.OrderRequestKeyRequired);
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var normalized = NormalizeRequest(request);
        var waiterId = GetCurrentUserId();
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                TableSessionId = tableSessionId, WaiterUserId = waiterId, normalized.Note,
                Items = normalized.Items.Select(line => new
                {
                    line.ItemId, line.Quantity, line.Note,
                    ExpectedUnitPrice = line.ExpectedUnitPrice?.ToString("G29", CultureInfo.InvariantCulture)
                })
            })));

        var snapshot = await _sessionAccess.GetMyOpenSessionAsync(restaurantId, tableSessionId, cancellationToken);
        await using var transaction = await _transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await _tables.AcquireReservationLockAsync(restaurantId, snapshot.TableId, cancellationToken);

        // Kilidi gözləyərkən sessiya bağlana və ya ofisiantın rolu dəyişə bilər; giriş yenidən yoxlanılır.
        var current = await _sessionAccess.GetMyOpenSessionAsync(restaurantId, tableSessionId, cancellationToken);
        if (current.TableId != snapshot.TableId)
            throw new BusinessRuleException(ErrorCode.OrderSessionChanged);

        var existing = await _orders.FindCreationRequestAsync(restaurantId, tableSessionId, requestId, cancellationToken);
        if (existing is not null)
        {
            if (existing.IsDeleted || existing.CreationRequestHash != hash)
                throw new BusinessRuleException(ErrorCode.OrderRequestKeyConflict);
            await transaction.CommitAsync(cancellationToken);
            return new CreateOrderResult(OrderResponseMapper.Map(existing,
                await CanCancelAsync(existing.Id, cancellationToken)), true);
        }

        var session = await _sessions.Query(session => session.Id == tableSessionId &&
            session.RestaurantId == restaurantId && session.WaiterUserId == waiterId &&
            session.StatusId == StatusIds.TableSession(TableSessionStatus.Open) && session.ClosedAt == null)
            .Include(session => session.Payments).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(ErrorCode.TableSessionNotFound);
        if (session.Payments.Any(payment => !payment.IsDeleted && payment.PaymentStatusId ==
            StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Pending)))
            throw new BusinessRuleException(ErrorCode.SessionBillPaymentPending);
        if (session.Payments.Any(payment => !payment.IsDeleted && payment.RequiresRefund))
            throw new BusinessRuleException(ErrorCode.SessionBillInvalid);
        var lines = await BuildLinesAsync(restaurantId, normalized.Items, cancellationToken);
        var order = new Domain.Entities.Order
        {
            RestaurantId = restaurantId, TableSessionId = session.Id, TableId = session.TableId,
            ReservationId = session.ReservationId, CustomerUserId = session.CustomerUserId,
            WaiterUserId = waiterId, SourceId = (int)OrderSource.Waiter,
            StatusId = StatusIds.Build(StatusType.Order, (int)OrderStatus.Created), OpenedAt = _clock.GetUtcNow().UtcDateTime,
            Note = normalized.Note, CreationRequestId = requestId, CreationRequestHash = hash, OrderItems = lines
        };
        await _orders.Add(order);
        await _orders.SaveChangesAsync();
        await _audit.RecordRestaurantActionAsync(restaurantId, AuditActions.OrderCreated,
            new { orderId = order.Id, tableSessionId, tableId = order.TableId, lineCount = lines.Count },
            AuditEntityTypes.Order, order.Id);
        await transaction.CommitAsync(cancellationToken);
        return new CreateOrderResult(OrderResponseMapper.Map(order,
            await CanCancelAsync(order.Id, cancellationToken)), false);
    }

    public async Task<PaginatedList<SessionOrderResponse>> GetForSessionAsync(int restaurantId, int tableSessionId,
        PaginationFilter pagination, CancellationToken cancellationToken = default)
    {
        await _sessionAccess.GetMyOpenSessionAsync(restaurantId, tableSessionId, cancellationToken);
        pagination = PaginationFilterNormalizer.Normalize(pagination);
        var query = QuerySessionOrders(restaurantId, tableSessionId);
        var count = await query.CountAsync(cancellationToken);
        var skip = (long)(pagination.PageNumber - 1) * pagination.PageSize;
        var orders = skip >= count ? [] : await query.OrderByDescending(order => order.OpenedAt)
            .ThenByDescending(order => order.Id).Skip((int)skip).Take(pagination.PageSize)
            .Include(order => order.OrderItems).ToListAsync(cancellationToken);
        var ids = orders.Select(order => order.Id).ToArray();
        var cancellableIds = await query.Where(OrderCancellationPolicy.Cancellable).Where(order => ids.Contains(order.Id))
            .Select(order => order.Id).ToListAsync(cancellationToken);
        return new PaginatedList<SessionOrderResponse>(orders.Select(order => OrderResponseMapper.Map(order, cancellableIds.Contains(order.Id))).ToList(),
            count, pagination.PageNumber, pagination.PageSize);
    }

    public async Task<SessionOrderResponse> GetByIdAsync(int restaurantId, int tableSessionId, int orderId,
        CancellationToken cancellationToken = default)
    {
        await _sessionAccess.GetMyOpenSessionAsync(restaurantId, tableSessionId, cancellationToken);
        if (orderId <= 0)
            throw new BadRequestException(ErrorCode.InvalidOrderId);
        var order = await QuerySessionOrders(restaurantId, tableSessionId).Include(order => order.OrderItems)
            .SingleOrDefaultAsync(order => order.Id == orderId, cancellationToken)
            ?? throw new NotFoundException(ErrorCode.OrderNotFound);
        var canCancel = await QuerySessionOrders(restaurantId, tableSessionId).Where(OrderCancellationPolicy.Cancellable)
            .AnyAsync(candidate => candidate.Id == orderId, cancellationToken);
        return OrderResponseMapper.Map(order, canCancel);
    }

    /// <summary>Sifariş oxunuşunu cari ofisiantın açıq sessiyasına bağlayır; ilkin girişdən sonrakı aidiyyət və bağlanma dəyişikliklərini də filtrdə qoruyur.</summary>
    private IQueryable<Domain.Entities.Order> QuerySessionOrders(int restaurantId, int sessionId)
    {
        var waiterId = GetCurrentUserId();
        return _orders.Query(order => order.RestaurantId == restaurantId && order.TableSessionId == sessionId &&
            order.TableSession != null && order.TableSession.RestaurantId == restaurantId &&
            order.TableSession.WaiterUserId == waiterId && order.TableSession.ClosedAt == null &&
            order.TableSession.StatusId == StatusIds.TableSession(TableSessionStatus.Open));
    }

    /// <summary>Yaratma cavabında da serverin ortaq SQL qaydası ilə ləğv düyməsinin mümkünlüyünü hesablayır.</summary>
    private Task<bool> CanCancelAsync(int orderId, CancellationToken token)
        => _orders.Query(order => order.Id == orderId).Where(OrderCancellationPolicy.Cancellable).AnyAsync(token);

    /// <summary>Eyni məhsul, qeyd və gözlənilən qiymət sətirlərini birləşdirir; sıralama fərqinin təkrar sorğunun hash-ini dəyişməsinin qarşısını alır.</summary>
    private NormalizedRequest NormalizeRequest(CreateWaiterOrderRequest request)
    {
        var lines = request.Items.GroupBy(line => new { line.ItemId, Note = NormalizeNote(line.Note), line.ExpectedUnitPrice })
            .Select(group => new NormalizedLine(group.Key.ItemId, group.Sum(line => line.Quantity),
                group.Key.Note, group.Key.ExpectedUnitPrice))
            .OrderBy(line => line.ItemId).ThenBy(line => line.Note, StringComparer.Ordinal)
            .ThenBy(line => line.ExpectedUnitPrice).ToList();
        if (lines.GroupBy(line => line.ItemId).Any(group => group.Sum(line => line.Quantity) > _limits.MaxQuantityPerItem))
            throw new BadRequestException(ErrorCode.InvalidOrderRequest);
        return new NormalizedRequest(NormalizeNote(request.Note), lines);
    }

    /// <summary>Menyu mövcudluğunu və restoran aidiyyətini bir sorğuda yoxlayır, məhsul adını və qiymətini sifarişdə sabit nüsxə kimi saxlayır. Stokdan silinmə və mətbəxə ötürmə etmir.</summary>
    private async Task<List<Domain.Entities.OrderItem>> BuildLinesAsync(int restaurantId,
        IReadOnlyCollection<NormalizedLine> lines, CancellationToken cancellationToken)
    {
        var ids = lines.Select(line => line.ItemId).Distinct().ToArray();
        var products = await _items.Query(item => item.RestaurantId == restaurantId && ids.Contains(item.Id) &&
            item.IsActive && item.IsAvailable && item.Category.IsActive && item.Category.RestaurantId == restaurantId &&
            (item.StatusId == StatusIds.Item(ItemStatus.Available) || item.StatusId == StatusIds.Item(ItemStatus.Limited)))
            .Select(item => new { item.Id, item.Name, item.BasePrice }).ToDictionaryAsync(item => item.Id, cancellationToken);
        if (products.Count != ids.Length)
            throw new BusinessRuleException(ErrorCode.OrderItemUnavailable);

        var result = new List<Domain.Entities.OrderItem>();
        foreach (var line in lines)
        {
            var product = products[line.ItemId];
            if (line.ExpectedUnitPrice.HasValue && line.ExpectedUnitPrice.Value != product.BasePrice)
                throw new BusinessRuleException(ErrorCode.OrderItemPriceChanged);
            if (string.IsNullOrWhiteSpace(product.Name) || product.Name.Length > 200 || product.BasePrice < 0 ||
                product.BasePrice > MaxStoredAmount || decimal.Round(product.BasePrice, 2) != product.BasePrice)
                throw new BusinessRuleException(ErrorCode.OrderItemUnavailable);
            var lineTotal = product.BasePrice * line.Quantity;
            if (lineTotal > MaxStoredAmount)
                throw new BusinessRuleException(ErrorCode.OrderItemUnavailable);
            result.Add(new Domain.Entities.OrderItem
            {
                ItemId = product.Id, NameSnapshot = product.Name, Quantity = line.Quantity,
                UnitPrice = product.BasePrice, UnitPriceSnapshot = product.BasePrice,
                LineTotal = lineTotal, Note = line.Note
            });
        }
        return result;
    }

    /// <summary>Boş qeydləri null-a çevirir və kənar boşluqları silərək müqayisə və saxlanmanı eyniləşdirir.</summary>
    private static string? NormalizeNote(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record NormalizedLine(int ItemId, int Quantity, string? Note, decimal? ExpectedUnitPrice);
    private sealed record NormalizedRequest(string? Note, List<NormalizedLine> Items);
}
