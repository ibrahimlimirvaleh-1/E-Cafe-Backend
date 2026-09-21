using System.Data;
using System.Text.Json;
using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Repositories.File;
using ECafe.Application.Repositories.Reservation;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.RestaurantContract;
using ECafe.Application.Repositories.ReservationPaymentInstruction;
using ECafe.Application.Repositories.ReservationPaymentProof;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Repository;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.FileAccess.Abstract;
using ECafe.Application.Services.Reservation.Abstract;
using ECafe.Application.Services.Workflow.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Workflow;
using ECafe.Shared.DTOs;
using ECafe.Shared.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StatusTypeEnum = ECafe.Domain.Enums.StatusType;

namespace ECafe.Application.Services.Reservation.Concrete;

public sealed class ReservationManager : BaseManager, IReservationService
{
    private const int DefaultHoldMinutes = 15;
    private const string SubmitPaymentProofActionCode = "submitPaymentProof";
    private static string ReservationFlowCode
        => WorkflowFlowCode.FromStatusType(StatusTypeEnum.Reservation);

    private static readonly int[] ReservationManagerRoleIds =
    [
        (int)RoleCode.Manager
    ];

    private readonly ITableRepository _tableRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IRestaurantContractRepository _restaurantContractRepository;
    private readonly IReservationRepository _reservationRepository;
    private readonly IUserRestaurantRepository _userRestaurantRepository;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLogService;
    private readonly IApplicationDbTransactionFactory _transactionFactory;
    private readonly IReservationPaymentInstructionRepository _reservationPaymentInstructionRepository;
    private readonly IReservationPaymentProofRepository _reservationPaymentProofRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IFileAccessUrlService _fileAccessUrlService;
    private readonly IWorkflowActionService _workflowActionService;

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
        IAuditLogService auditLogService,
        IApplicationDbTransactionFactory transactionFactory,
        IReservationPaymentInstructionRepository reservationPaymentInstructionRepository,
        IReservationPaymentProofRepository reservationPaymentProofRepository,
        IFileRepository fileRepository,
        IFileAccessUrlService fileAccessUrlService,
        IWorkflowActionService workflowActionService)
        : base(httpContextAccessor, mapper, configuration)
    {
        _tableRepository = tableRepository;
        _restaurantRepository = restaurantRepository;
        _restaurantContractRepository = restaurantContractRepository;
        _reservationRepository = reservationRepository;
        _userRestaurantRepository = userRestaurantRepository;
        _notificationService = notificationService;
        _auditLogService = auditLogService;
        _transactionFactory = transactionFactory;
        _reservationPaymentInstructionRepository = reservationPaymentInstructionRepository;
        _reservationPaymentProofRepository = reservationPaymentProofRepository;
        _fileRepository = fileRepository;
        _fileAccessUrlService = fileAccessUrlService;
        _workflowActionService = workflowActionService;
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
        await NotifyRestaurantResponsibleUserAsync(restaurantId, table, reservation);

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

    public async Task<PaginatedList<ReservationResponse>> GetMyReservationsAsync(
        ReservationQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var reservations = await _reservationRepository.GetForCustomerAsync(
            GetCurrentUserId(),
            request,
            cancellationToken);

        return MapPage(reservations, request);
    }

    public async Task<PaginatedList<ReservationResponse>> GetRestaurantReservationsAsync(
        int restaurantId,
        ReservationQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureRestaurantReservationAccessAsync(restaurantId);
        var reservations = await _reservationRepository.GetForRestaurantAsync(
            restaurantId,
            request,
            cancellationToken);

        return MapPage(reservations, request);
    }

    public async Task<ReservationResponse> GetRestaurantReservationByIdAsync(
        int restaurantId,
        int reservationId,
        CancellationToken cancellationToken = default)
    {
        ValidateRestaurantId(restaurantId);
        if (reservationId <= 0)
            throw new BadRequestException("Rezervasiya seçimi düzgün deyil.");

        await EnsureRestaurantReservationAccessAsync(restaurantId);
        var reservation = await _reservationRepository.GetByIdForRestaurantAsync(
            reservationId,
            restaurantId,
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


    public async Task<PaymentInstructionResponse> SendPaymentInstructionAsync(int restaurantId, int reservationId, PaymentInstructionRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.DisplayText))
            throw new BadRequestException("Ödəniş məlumatı boş ola bilməz.");

        var displayText = request.DisplayText.Trim();
        if (displayText.Length > 1000)
            throw new BadRequestException("Ödəniş məlumatı 1000 simvoldan çox ola bilməz.");

        var userId = GetCurrentUserId();

        var now = DateTime.UtcNow;

        int paymentPendingStatusId = StatusIds.Reservation(ReservationStatus.PendingPayment);

        var userBelongsToRestaurant = await _userRestaurantRepository.UserBelogsToRestaurantAsync(userId, restaurantId);

        if (!userBelongsToRestaurant)
            throw new BusinessRuleException(ErrorCode.UserNotBelongsToRestaurant);

        var activeRoleId = await _userRestaurantRepository.GetActiveRoleIdAsync(userId, restaurantId);
        if (activeRoleId is not ((int)RoleCode.Owner) and not ((int)RoleCode.Manager))
            throw new ForbiddenException(ErrorCode.OnlyRestaurantManagersCanSendPaymentInstruction);


        var reservation = await _reservationRepository.GetByIdForRestaurantAsync(
            reservationId,
            restaurantId,
            cancellationToken);

        if (reservation is null)
            throw new BusinessRuleException(ErrorCode.ReservationNotBelongsToRestaurant);

        if (reservation.StatusId != paymentPendingStatusId ||
            reservation.HoldExpiresAt is null ||
            reservation.HoldExpiresAt <= now)
            throw new BusinessRuleException(ErrorCode.ThisOperatioCannotBePerformedForThisReservation);

        await _workflowActionService.EnsureCanExecuteAsync(
            ReservationFlowCode,
            reservation.StatusId,
            "sendPaymentInstruction",
            restaurantId,
            reservation.Id);

        var paymentInstruction = new ReservationPaymentInstruction
        {
            ReservationId = reservation.Id,
            DisplayText = displayText,
            Amount = reservation.DepositAmount,
            SentByUserId = userId,
            SentAt = now
        };

        await using var transaction = await _transactionFactory.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        await _reservationPaymentInstructionRepository.Add(paymentInstruction);
        await _reservationPaymentInstructionRepository.SaveChangesAsync();

        await _notificationService.CreateAsync(new CreateNotificationRequest
        {
            UserId = reservation.CustomerUserId,
            RestaurantId = restaurantId,
            Title = "Ödəniş məlumatı göndərildi",
            Message = "Restoran rezervasiyanız üçün ödəniş məlumatlarını göndərdi.",
            TypeId = (int)NotificationType.ReservationPaymentInstructionSent,
            ChannelId = (int)NotificationChannel.InApp,
            PayloadJson = JsonSerializer.Serialize(new
            {
                reservationId = reservation.Id,
                instructionId = paymentInstruction.Id
            }),
            RelatedEntityType = AuditEntityTypes.Reservation,
            RelatedEntityId = reservation.Id
        });

        await _auditLogService.RecordRestaurantActionAsync(
            restaurantId,
            "PaymentInstructionSent",
            new
            {
                reservationId = reservation.Id,
                instructionId = paymentInstruction.Id,
                amount = paymentInstruction.Amount
            },
            AuditEntityTypes.Reservation,
            reservation.Id);

        await transaction.CommitAsync(cancellationToken);

        return new PaymentInstructionResponse
        {
            Id = paymentInstruction.Id,
            ReservationId = reservation.Id,
            Status = reservation.Status?.Name ?? ReservationStatus.PendingPayment.ToString(),
            DisplayText = paymentInstruction.DisplayText,
            Amount = paymentInstruction.Amount,
            SentAt = paymentInstruction.SentAt
        };
    }

    public async Task<PaymentProofResponse> SubmitPaymentProofAsync(
        int restaurantId,
        int reservationId,
        int fileId,
        CancellationToken cancellationToken = default)
    {
        ValidateRestaurantId(restaurantId);

        if (reservationId <= 0 || fileId <= 0)
            throw new BadRequestException("Rezervasiya və çek məlumatları düzgün deyil.");

        var userId = GetCurrentUserId();
        var now = DateTime.UtcNow;
        var paymentSubmittedStatusId = StatusIds.Reservation(ReservationStatus.PaymentSubmitted);

        await using var transaction = await _transactionFactory.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var reservation = await _reservationRepository.GetByIdForCustomerForUpdateAsync(
            reservationId,
            restaurantId,
            userId,
            cancellationToken);

        if (reservation is null)
            throw new NotFoundException(ErrorCode.ReservationNotFound);

        await _tableRepository.AcquireReservationLockAsync(
            restaurantId,
            reservation.TableId,
            cancellationToken);

        await EnsurePaymentProofSubmissionAllowedAsync(
            reservation,
            cancellationToken);

        var file = await _fileRepository.GetAttachableByIdAsync(fileId);
        if (file is null ||
            file.FileTypeId != (int)FileTypeCode.PaymentReceipt ||
            !string.Equals(file.CreatedBy, userId.ToString(), StringComparison.Ordinal))
        {
            throw new BusinessRuleException(ErrorCode.FileNotFoundOrAlreadyAttached);
        }

        var paymentProof = new ReservationPaymentProof
        {
            ReservationId = reservation.Id,
            FileId = file.Id,
            Amount = reservation.DepositAmount,
            StatusId = paymentSubmittedStatusId,
            SubmittedAt = now
        };

        reservation.StatusId = paymentSubmittedStatusId;
        reservation.PaymentSubmittedAt = now;
        reservation.HoldExpiresAt = null;

        await _reservationPaymentProofRepository.Add(paymentProof);
        await _reservationRepository.SaveChangesAsync();
        await NotifyPaymentProofSubmittedAsync(restaurantId, reservation, paymentProof.Id);

        await _auditLogService.RecordRestaurantActionAsync(
            restaurantId,
            AuditActions.ReservationPaymentProofSubmitted,
            new
            {
                reservationId = reservation.Id,
                paymentProofId = paymentProof.Id,
                fileId = file.Id,
                amount = paymentProof.Amount
            },
            AuditEntityTypes.Reservation,
            reservation.Id);

        await transaction.CommitAsync(cancellationToken);

        return new PaymentProofResponse
        {
            Id = paymentProof.Id,
            ReservationId = paymentProof.ReservationId,
            FileId = paymentProof.FileId,
            Amount = paymentProof.Amount,
            Status = ReservationStatus.PaymentSubmitted.GetName(),
            SubmittedAt = paymentProof.SubmittedAt,
            FileViewUrl = _fileAccessUrlService.BuildViewUrl(paymentProof.FileId)
        };
    }

    public async Task EnsurePaymentProofCanBeSubmittedAsync(
        int restaurantId,
        int reservationId,
        CancellationToken cancellationToken = default)
    {
        ValidateRestaurantId(restaurantId);

        if (reservationId <= 0)
            throw new BadRequestException("Rezervasiya seçimi düzgün deyil.");

        var reservation = await _reservationRepository.GetByIdForCustomerAsync(
            reservationId,
            GetCurrentUserId(),
            cancellationToken);

        if (reservation is null || reservation.RestaurantId != restaurantId)
            throw new NotFoundException(ErrorCode.ReservationNotFound);

        await EnsurePaymentProofSubmissionAllowedAsync(
            reservation,
            cancellationToken);
    }

    private async Task NotifyPaymentProofSubmittedAsync(
        int restaurantId,
        Domain.Entities.Reservation reservation,
        int paymentProofId)
    {
        var assignments = await GetRestaurantResponsibleAssignmentsAsync(restaurantId);

        foreach (var assignment in assignments)
        {
            await _notificationService.CreateAsync(new CreateNotificationRequest
            {
                UserId = assignment.UserId,
                RestaurantId = restaurantId,
                Title = "Ödəniş çeki göndərildi",
                Message = $"Rezervasiya #{reservation.Id} üçün müştəri ödəniş çekini göndərdi.",
                TypeId = (int)NotificationType.ReservationPaymentProofSubmitted,
                ChannelId = (int)NotificationChannel.InApp,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    restaurantId,
                    reservationId = reservation.Id,
                    paymentProofId
                }),
                RelatedEntityType = AuditEntityTypes.Reservation,
                RelatedEntityId = reservation.Id
            });
        }
    }

    private async Task EnsurePaymentProofSubmissionAllowedAsync(
        Domain.Entities.Reservation reservation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pendingPaymentStatusId = StatusIds.Reservation(ReservationStatus.PendingPayment);

        if (reservation.StatusId != pendingPaymentStatusId ||
            reservation.HoldExpiresAt is null ||
            reservation.HoldExpiresAt <= DateTime.UtcNow)
        {
            throw new BusinessRuleException(ErrorCode.ThisOperatioCannotBePerformedForThisReservation);
        }

        if (!reservation.PaymentInstructions.Any())
            throw new BusinessRuleException("Restoran hələ ödəniş məlumatı göndərməyib.");

        await _workflowActionService.EnsureCanExecuteAsync(
            ReservationFlowCode,
            reservation.StatusId,
            SubmitPaymentProofActionCode,
            null,
            reservation.Id);
    }

    private async Task<List<Domain.Entities.UserRestaurant>> GetRestaurantResponsibleAssignmentsAsync(
        int restaurantId)
    {
        var assignments = await _userRestaurantRepository.GetActiveByRestaurantAndRolesAsync(
            restaurantId,
            ReservationManagerRoleIds);

        if (assignments.Count > 0)
            return assignments;

        var ownerAssignment = await _userRestaurantRepository
            .GetActiveOwnerByRestaurantAsync(restaurantId);

        if (ownerAssignment is not null)
            assignments.Add(ownerAssignment);

        return assignments;
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

    private async Task NotifyRestaurantResponsibleUserAsync(
        int restaurantId,
        Domain.Entities.Table table,
        Domain.Entities.Reservation reservation)
    {
        var assignments = await GetRestaurantResponsibleAssignmentsAsync(restaurantId);

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
        var latestPaymentInstruction = reservation.PaymentInstructions
            .OrderByDescending(instruction => instruction.SentAt)
            .FirstOrDefault();

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
            CancellationDeadline = ToUtcOffset(reservation.CancellationDeadline),
            RestaurantName = reservation.Restaurant?.Name,
            TableName = reservation.Table is null
                ? null
                : reservation.Table.Name ?? $"Masa {reservation.Table.TableNo}",
            CustomerName = reservation.CustomerUser is null
                ? null
                : $"{reservation.CustomerUser.Name} {reservation.CustomerUser.Surname}".Trim(),
            LatestPaymentInstruction = latestPaymentInstruction is null
                ? null
                : new PaymentInstructionResponse
                {
                    Id = latestPaymentInstruction.Id,
                    ReservationId = reservation.Id,
                    Status = reservation.Status?.Name ?? ReservationStatus.PendingPayment.ToString(),
                    DisplayText = latestPaymentInstruction.DisplayText,
                    Amount = latestPaymentInstruction.Amount,
                    SentAt = latestPaymentInstruction.SentAt
                }
        };
    }

    private static PaginatedList<ReservationResponse> MapPage(
        PaginatedList<Domain.Entities.Reservation> page,
        ReservationQueryRequest request)
    {
        var items = page.Items.Select(MapResponse).ToList();
        return new PaginatedList<ReservationResponse>(
            items,
            page.TotalCount,
            page.PageIndex,
            Math.Clamp(request.PageSize, 1, 100));
    }

    private async Task EnsureRestaurantReservationAccessAsync(int restaurantId)
    {
        ValidateRestaurantId(restaurantId);

        if (IsCurrentUserSuperAdmin())
            return;

        var userId = GetCurrentUserId();
        if (!await _userRestaurantRepository.UserBelogsToRestaurantAsync(userId, restaurantId))
            throw new BusinessRuleException(ErrorCode.UserNotBelongsToRestaurant);

        var roleId = GetCurrentRoleId(restaurantId);
        if (!ReservationManagerRoleIds.Contains(roleId))
            throw new ForbiddenException(ErrorCode.OnlyRestaurantManagersCanSendPaymentInstruction);
    }

    private static DateTimeOffset? ToUtcOffset(DateTime? value)
    {
        return value.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc), TimeSpan.Zero)
            : null;
    }


}
