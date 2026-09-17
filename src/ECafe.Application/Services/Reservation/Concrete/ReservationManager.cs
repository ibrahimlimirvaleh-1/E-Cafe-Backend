using System.Data;
using System.Text.Json;
using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Repositories.Reservation;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.RestaurantContract;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Repository;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.Reservation.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Reservation.Concrete;

public sealed class ReservationManager : BaseManager, IReservationService
{
    private const int DefaultHoldMinutes = 15;

    private static readonly int[] ReservationManagerRoleIds =
    [
        (int)RoleCode.Owner,
        (int)RoleCode.Manager
    ];

    private readonly ITableRepository _tableRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IRestaurantContractRepository _restaurantContractRepository;
    private readonly IReservationRepository _reservationRepository;
    private readonly IUserRestaurantRepository _userRestaurantRepository;
    private readonly INotificationService _notificationService;
    private readonly IApplicationDbTransactionFactory _transactionFactory;

    public ReservationManager(
        IHttpContextAccessor httpContextAccessor,
        IMapper mapper,
        IConfiguration configuration,
        ITableRepository tableRepository,
        IRestaurantRepository restaurantRepository,
        IRestaurantContractRepository restaurantContractRepository,
        IReservationRepository reservationRepository,
        IUserRestaurantRepository userRestaurantRepository,
        INotificationService notificationService,
        IApplicationDbTransactionFactory transactionFactory)
        : base(httpContextAccessor, mapper, configuration)
    {
        _tableRepository = tableRepository;
        _restaurantRepository = restaurantRepository;
        _restaurantContractRepository = restaurantContractRepository;
        _reservationRepository = reservationRepository;
        _userRestaurantRepository = userRestaurantRepository;
        _notificationService = notificationService;
        _transactionFactory = transactionFactory;
    }

    public async Task<ReservationResponse> CreateReservationAsync(
        int restaurantId,
        CreateReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRestaurantId(restaurantId);

        var userId = GetCurrentUserId();
        var restaurant = await GetReservableRestaurantAsync(restaurantId);
        ValidateReservationTime(request.ReservedAt);
        await EnsureRestaurantIsOpenAsync(restaurantId, request.ReservedAt);

        var table = await GetReservableTableAsync(
            restaurantId,
            request.TableId,
            request.PeopleCount);

        await using var transaction = await _transactionFactory.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        // The lock makes concurrent requests for the same table run one at a time.
        await _tableRepository.AcquireReservationLockAsync(
            restaurantId,
            request.TableId,
            cancellationToken);

        await EnsureTableIsAvailableAsync(restaurantId, request.TableId, request.ReservedAt);

        var reservation = BuildReservation(restaurantId, userId, restaurant, request);
        await _reservationRepository.Add(reservation);
        await _reservationRepository.SaveChangesAsync();
        await NotifyRestaurantManagersAsync(restaurantId, table, reservation);

        await transaction.CommitAsync(cancellationToken);
        return MapResponse(reservation);
    }

    public async Task<ReservationResponse> GetReservationByIdAsync(
        int reservationId,
        CancellationToken cancellationToken = default)
    {
        if (reservationId <= 0)
            throw new BadRequestException("Rezervasiya seçimi düzgün deyil.");

        var userId = GetCurrentUserId();
        var reservation = await _reservationRepository.GetByIdForCustomerAsync(
            reservationId,
            userId,
            cancellationToken);

        if (reservation is null)
            throw new NotFoundException(ErrorCode.ReservationNotFound);

        return MapResponse(reservation);
    }


    public async Task<int> ExpirePendingReservationsAsync(int batchSize, CancellationToken cancellationToken)
    {
        return await _reservationRepository.ExpirePendingPaymentsAsync(
            DateTime.UtcNow,
            batchSize,
            cancellationToken);
    }
    private async Task<Domain.Entities.Restaurant> GetReservableRestaurantAsync(int restaurantId)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(restaurantId);
        if (restaurant is null || !restaurant.IsActive)
            throw new BusinessRuleException(ErrorCode.RestaurantNotFound);

        if (!await _restaurantContractRepository.HasActiveContractAsync(restaurantId))
            throw new BusinessRuleException(ErrorCode.RestaurantActiveContractRequired);

        return restaurant;
    }

    private async Task<Domain.Entities.Table> GetReservableTableAsync(
        int restaurantId,
        int tableId,
        int peopleCount)
    {
        var table = await _tableRepository.GetByIdAsync(tableId);
        if (table is null)
            throw new BusinessRuleException(ErrorCode.TableNotFound);

        if (!table.IsActive || table.RestaurantId != restaurantId)
            throw new BusinessRuleException(ErrorCode.TableNotBelongToRestaurant);

        if (table.Capacity < peopleCount)
            throw new BusinessRuleException(ErrorCode.PeopleCountMoreThanTableCapacity);

        return table;
    }

    private async Task EnsureRestaurantIsOpenAsync(int restaurantId, DateTimeOffset reservedAt)
    {
        if (!await _restaurantRepository.IsRestaurantOpenAsync(restaurantId, reservedAt))
            throw new BusinessRuleException(ErrorCode.RestaurantClosedForReservation);
    }

    private async Task EnsureTableIsAvailableAsync(
        int restaurantId,
        int tableId,
        DateTimeOffset reservedAt)
    {
        if (await _tableRepository.HasOpenTableSessionAsync(restaurantId, tableId))
            throw new BusinessRuleException(ErrorCode.TableAlreadyReserved);

        if (!await _tableRepository.IsTableAvailableForReservationAsync(restaurantId, tableId, reservedAt))
            throw new BusinessRuleException(ErrorCode.TableAlreadyReserved);
    }

    private Domain.Entities.Reservation BuildReservation(
        int restaurantId,
        int userId,
        Domain.Entities.Restaurant restaurant,
        CreateReservationRequest request)
    {
        var cancellationWindowMinutes = Math.Max(restaurant.CancellationWindowMinutes, 0);

        return new Domain.Entities.Reservation
        {
            RestaurantId = restaurantId,
            CustomerUserId = userId,
            TableId = request.TableId,
            PeopleCount = request.PeopleCount,
            StatusId = StatusIds.Reservation(ReservationStatus.PendingPayment),
            DepositAmount = restaurant.DepositAmount,
            CancellationWindowMinutes = cancellationWindowMinutes,
            CancellationDeadline = request.ReservedAt.UtcDateTime.AddMinutes(-cancellationWindowMinutes),
            ReservedAt = request.ReservedAt.UtcDateTime,
            HoldExpiresAt = DateTime.UtcNow.AddMinutes(GetHoldMinutes()),
            Note = request.Note?.Trim()
        };
    }

    private async Task NotifyRestaurantManagersAsync(
        int restaurantId,
        Domain.Entities.Table table,
        Domain.Entities.Reservation reservation)
    {
        var assignments = await _userRestaurantRepository.GetActiveByRestaurantAndRolesAsync(
            restaurantId,
            ReservationManagerRoleIds);

        foreach (var assignment in assignments)
        {
            await _notificationService.CreateAsync(new CreateNotificationRequest
            {
                UserId = assignment.UserId,
                RestaurantId = restaurantId,
                Title = "Yeni rezervasiya yaradıldı",
                Message = $"Masa {table.Name ?? table.TableNo.ToString()} üçün yeni rezervasiya ödəniş gözləyir.",
                TypeId = (int)NotificationType.ReservationCreated,
                ChannelId = (int)NotificationChannel.InApp,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    restaurantId,
                    reservationId = reservation.Id,
                    tableId = reservation.TableId
                }),
                RelatedEntityType = AuditEntityTypes.Reservation,
                RelatedEntityId = reservation.Id
            });
        }
    }

    private int GetHoldMinutes()
    {
        return int.TryParse(_configuration["Reservations:HoldMinutes"], out var configuredMinutes)
            ? Math.Max(configuredMinutes, 1)
            : DefaultHoldMinutes;
    }

    private static void ValidateRestaurantId(int restaurantId)
    {
        if (restaurantId <= 0)
            throw new BusinessRuleException(ErrorCode.InvalidRestaurantId);
    }

    private static void ValidateReservationTime(DateTimeOffset reservedAt)
    {
        if (reservedAt <= DateTimeOffset.UtcNow)
            throw new BadRequestException("Rezervasiya vaxtı gələcək tarix olmalıdır.");
    }

    private static ReservationResponse MapResponse(Domain.Entities.Reservation reservation)
    {
        return new ReservationResponse
        {
            Id = reservation.Id,
            RestaurantId = reservation.RestaurantId,
            TableId = reservation.TableId,
            ReservedAt = ToUtcOffset(reservation.ReservedAt)!.Value,
            PeopleCount = reservation.PeopleCount,
            StatusId = reservation.StatusId,
            Status = reservation.Status?.Name ?? ReservationStatus.PendingPayment.ToString(),
            DepositAmount = reservation.DepositAmount,
            HoldExpiresAt = ToUtcOffset(reservation.HoldExpiresAt),
            CancellationDeadline = ToUtcOffset(reservation.CancellationDeadline)
        };
    }

    private static DateTimeOffset? ToUtcOffset(DateTime? value)
    {
        return value.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc), TimeSpan.Zero)
            : null;
    }


}
