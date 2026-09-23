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
    private const int DefaultRestaurantResponseMinutes = 15;
    private const string SubmitPaymentProofActionCode = "submitPaymentProof";
    private static string ReservationFlowCode
        => WorkflowFlowCode.FromStatusType(StatusTypeEnum.Reservation);

    private static readonly int[] ReservationManagerRoleIds =
    [
        (int)RoleCode.Manager,
        (int)RoleCode.Owner
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
        reservation.StatusHistory.Add(new ReservationStatusHistory
        {
            ToStatusId = reservation.StatusId,
            ChangedByUserId = userId,
            ChangedAt = DateTime.UtcNow,
            Reason = "Rezervasiya yaradıldı."
        });
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

    public async Task<ReservationHistoryResponse> GetReservationHistoryAsync(
        int reservationId,
        CancellationToken cancellationToken = default)
    {
        if (reservationId <= 0)
            throw new BadRequestException("Rezervasiya seçimi düzgün deyil.");

        var reservation = await _reservationRepository.GetByIdForCustomerWithHistoryAsync(
            reservationId,
            GetCurrentUserId(),
            cancellationToken);

        if (reservation is null)
            throw new NotFoundException(ErrorCode.ReservationNotFound);

        return MapHistoryResponse(reservation);
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

    public async Task<ReservationHistoryResponse> GetRestaurantReservationHistoryAsync(
        int restaurantId,
        int reservationId,
        CancellationToken cancellationToken = default)
    {
        ValidateRestaurantId(restaurantId);
        if (reservationId <= 0)
            throw new BadRequestException("Rezervasiya seçimi düzgün deyil.");

        await EnsureRestaurantReservationAccessAsync(restaurantId);
        var reservation = await _reservationRepository.GetByIdForRestaurantWithHistoryAsync(
            reservationId,
            restaurantId,
            cancellationToken);

        if (reservation is null)
            throw new NotFoundException(ErrorCode.ReservationNotFound);

        return MapHistoryResponse(reservation);
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

        int awaitingPaymentInstructionStatusId = StatusIds.Reservation(ReservationStatus.AwaitingPaymentInstruction);
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

        var previousStatusId = reservation.StatusId;

        var canSendInstruction = reservation.StatusId == awaitingPaymentInstructionStatusId &&
            reservation.RestaurantResponseExpiresAt is not null &&
            reservation.RestaurantResponseExpiresAt > now;
        var isLegacyPendingPayment = reservation.StatusId == paymentPendingStatusId &&
            (reservation.HoldExpiresAt is null || reservation.HoldExpiresAt > now);

        if (!canSendInstruction && !isLegacyPendingPayment)
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

        await _tableRepository.AcquireReservationLockAsync(
            restaurantId,
            reservation.TableId,
            cancellationToken);

        if (reservation.StatusId == awaitingPaymentInstructionStatusId)
        {
            await EnsureTableIsAvailableAsync(
                restaurantId,
                reservation.TableId,
                reservation.ReservedAt,
                reservation.Id);

            reservation.StatusId = paymentPendingStatusId;
            reservation.RestaurantResponseExpiresAt = null;
            reservation.HoldExpiresAt = now.AddMinutes(GetHoldMinutes());
        }
        else if (reservation.HoldExpiresAt is null)
        {
            await EnsureTableIsAvailableAsync(
                restaurantId,
                reservation.TableId,
                reservation.ReservedAt,
                reservation.Id);

            reservation.HoldExpiresAt = now.AddMinutes(GetHoldMinutes());
        }

        if (reservation.StatusId != previousStatusId)
        {
            reservation.StatusHistory.Add(new ReservationStatusHistory
            {
                FromStatusId = previousStatusId,
                ToStatusId = reservation.StatusId,
                ChangedByUserId = userId,
                ChangedAt = now,
                Reason = "Restoran ödəniş məlumatlarını göndərməyə başladı."
            });
        }

        await _reservationPaymentInstructionRepository.Add(paymentInstruction);
        await _reservationRepository.SaveChangesAsync();

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
            Status = reservation.StatusId == paymentPendingStatusId
                ? ReservationStatus.PendingPayment.GetName()
                : reservation.Status?.Name ?? ReservationStatus.PendingPayment.ToString(),
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

        var previousStatusId = reservation.StatusId;
        reservation.StatusId = paymentSubmittedStatusId;
        reservation.PaymentSubmittedAt = now;
        reservation.HoldExpiresAt = null;
        reservation.StatusHistory.Add(new ReservationStatusHistory
        {
            FromStatusId = previousStatusId,
            ToStatusId = paymentSubmittedStatusId,
            ChangedByUserId = userId,
            ChangedAt = now,
            Reason = "Müştəri ödəniş çekini göndərdi."
        });

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
            reservation.RestaurantId,
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
        DateTimeOffset reservedAt,
        int? excludedReservationId = null)
    {
        if (await _tableRepository.HasOpenTableSessionAsync(restaurantId, tableId))
            throw new BusinessRuleException(ErrorCode.TableAlreadyReserved);

        if (!await _tableRepository.IsTableAvailableForReservationAsync(
                restaurantId,
                tableId,
                reservedAt,
                excludedReservationId))
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
            StatusId = StatusIds.Reservation(ReservationStatus.AwaitingPaymentInstruction),
            DepositAmount = restaurant.DepositAmount,
            CancellationWindowMinutes = cancellationWindowMinutes,
            CancellationDeadline = request.ReservedAt.UtcDateTime.AddMinutes(-cancellationWindowMinutes),
            ReservedAt = request.ReservedAt.UtcDateTime,
            HoldExpiresAt = null,
            RestaurantResponseExpiresAt = DateTime.UtcNow.AddMinutes(GetRestaurantResponseMinutes()),
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

    private int GetRestaurantResponseMinutes()
    {
        return int.TryParse(_configuration["Reservations:RestaurantResponseMinutes"], out var configuredMinutes)
            ? Math.Max(configuredMinutes, 1)
            : DefaultRestaurantResponseMinutes;
    }

    private static void ValidateRestaurantId(int restaurantId)
    {
        if (restaurantId <= 0)
            throw new BusinessRuleException(ErrorCode.InvalidRestaurantId);
    }

    private static void ValidateReservationId(int reservationId)
    {
        if (reservationId <= 0)
            throw new BadRequestException("Rezervasiya seçimi düzgün deyil.");
    }

    private static string NormalizeReason(string? reason, string fallback)
    {
        var normalized = reason?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static string NormalizeRequiredReason(string? reason, string errorMessage)
    {
        var normalized = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new BadRequestException(errorMessage);

        if (normalized.Length > 500)
            throw new BadRequestException("Səbəb 500 simvoldan çox ola bilməz.");

        return normalized;
    }

    private Task<Domain.Entities.ReservationPaymentProof?> GetLatestPaymentProofForReviewAsync(
        int reservationId,
        int submittedStatusId,
        CancellationToken cancellationToken)
    {
        return _reservationPaymentProofRepository
            .QueryTracked(proof => proof.ReservationId == reservationId &&
                                   proof.StatusId == submittedStatusId)
            .OrderByDescending(proof => proof.SubmittedAt)
            .ThenByDescending(proof => proof.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static ReservationActionResponse BuildActionResponse(
        int reservationId,
        int statusId,
        ReservationStatus status,
        string message)
    {
        return new ReservationActionResponse
        {
            ReservationId = reservationId,
            StatusId = statusId,
            Status = status.GetName(),
            Message = message
        };
    }

    private async Task NotifyReservationCustomerAsync(
        int restaurantId,
        Domain.Entities.Reservation reservation,
        NotificationType notificationType,
        string title,
        string message)
    {
        await _notificationService.CreateAsync(new CreateNotificationRequest
        {
            UserId = reservation.CustomerUserId,
            RestaurantId = restaurantId,
            Title = title,
            Message = message,
            TypeId = (int)notificationType,
            ChannelId = (int)NotificationChannel.InApp,
            PayloadJson = JsonSerializer.Serialize(new
            {
                restaurantId,
                reservationId = reservation.Id,
                statusId = reservation.StatusId
            }),
            RelatedEntityType = AuditEntityTypes.Reservation,
            RelatedEntityId = reservation.Id
        });
    }

    private async Task NotifyReservationResponsibleUsersAsync(
        int restaurantId,
        Domain.Entities.Reservation reservation,
        string title,
        string message)
    {
        var assignments = await GetRestaurantResponsibleAssignmentsAsync(restaurantId);

        foreach (var assignment in assignments)
        {
            await _notificationService.CreateAsync(new CreateNotificationRequest
            {
                UserId = assignment.UserId,
                RestaurantId = restaurantId,
                Title = title,
                Message = message,
                TypeId = (int)NotificationType.ReservationCancelled,
                ChannelId = (int)NotificationChannel.InApp,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    restaurantId,
                    reservationId = reservation.Id,
                    statusId = reservation.StatusId
                }),
                RelatedEntityType = AuditEntityTypes.Reservation,
                RelatedEntityId = reservation.Id
            });
        }
    }

    private static void ValidateReservationTime(DateTimeOffset reservedAt)
    {
        if (reservedAt <= DateTimeOffset.UtcNow)
            throw new BadRequestException("Rezervasiya vaxtı gələcək tarix olmalıdır.");
    }

    private ReservationResponse MapResponse(Domain.Entities.Reservation reservation)
    {
        var latestPaymentInstruction = reservation.PaymentInstructions
            .OrderByDescending(instruction => instruction.SentAt)
            .FirstOrDefault();
        var latestPaymentProof = reservation.PaymentProofs
            .OrderByDescending(proof => proof.SubmittedAt)
            .ThenByDescending(proof => proof.Id)
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
            WorkflowFlowCode = ReservationFlowCode,
            DepositAmount = reservation.DepositAmount,
            HoldExpiresAt = ToUtcOffset(reservation.HoldExpiresAt),
            RestaurantResponseExpiresAt = ToUtcOffset(reservation.RestaurantResponseExpiresAt),
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
                },
            LatestPaymentProof = latestPaymentProof is null
                ? null
                : new PaymentProofResponse
                {
                    Id = latestPaymentProof.Id,
                    ReservationId = reservation.Id,
                    FileId = latestPaymentProof.FileId,
                    Amount = latestPaymentProof.Amount,
                    Status = latestPaymentProof.Status?.Name ?? ReservationStatus.PaymentSubmitted.GetName(),
                    SubmittedAt = latestPaymentProof.SubmittedAt,
                    FileViewUrl = _fileAccessUrlService.BuildViewUrl(latestPaymentProof.FileId)
                }
        };
    }

    private static ReservationHistoryResponse MapHistoryResponse(
        Domain.Entities.Reservation reservation)
    {
        var items = reservation.StatusHistory
            .OrderBy(history => history.ChangedAt)
            .ThenBy(history => history.Id)
            .Select(history => new ReservationHistoryItemResponse
            {
                Id = history.Id,
                FromStatus = history.FromStatus?.Name,
                ToStatus = history.ToStatus?.Name ?? ReservationStatus.PendingPayment.GetName(),
                ChangedAt = ToUtcOffset(history.ChangedAt)!.Value,
                ActorType = history.ChangedByUserId is null
                    ? "System"
                    : history.ChangedByUserId == reservation.CustomerUserId
                        ? "Customer"
                        : "Restaurant",
                Reason = history.Reason
            })
            .ToList();

        return new ReservationHistoryResponse
        {
            ReservationId = reservation.Id,
            Items = items
        };
    }

    public async Task<ReservationActionResponse> ApprovePaymentProofAsync(
        int restaurantId,
        int reservationId,
        CancellationToken cancellationToken = default)
    {
        ValidateRestaurantId(restaurantId);
        ValidateReservationId(reservationId);
        await EnsureRestaurantReservationAccessAsync(restaurantId);

        var userId = GetCurrentUserId();
        var now = DateTime.UtcNow;
        var paymentSubmittedStatusId = StatusIds.Reservation(ReservationStatus.PaymentSubmitted);
        var confirmedStatusId = StatusIds.Reservation(ReservationStatus.Confirmed);

        await using var transaction = await _transactionFactory.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var snapshot = await _reservationRepository.GetByIdForRestaurantSnapshotAsync(
            reservationId,
            restaurantId,
            cancellationToken);

        if (snapshot is null)
            throw new NotFoundException(ErrorCode.ReservationNotFound);

        await _tableRepository.AcquireReservationLockAsync(
            restaurantId,
            snapshot.TableId,
            cancellationToken);

        var reservation = await _reservationRepository.GetByIdForRestaurantAsync(
            reservationId,
            restaurantId,
            cancellationToken);

        if (reservation is null)
            throw new NotFoundException(ErrorCode.ReservationNotFound);

        if (reservation.StatusId != paymentSubmittedStatusId)
            throw new BusinessRuleException("Bu rezervasiyanın ödəniş çeki təsdiq gözləmir.");

        await _workflowActionService.EnsureCanExecuteAsync(
            ReservationFlowCode,
            reservation.StatusId,
            "approvePaymentProof",
            restaurantId,
            reservation.Id);

        var paymentProof = await GetLatestPaymentProofForReviewAsync(
            reservation.Id,
            paymentSubmittedStatusId,
            cancellationToken);

        if (paymentProof is null)
            throw new BusinessRuleException("Təsdiqlənəcək ödəniş çeki tapılmadı.");

        var previousStatusId = reservation.StatusId;
        paymentProof.StatusId = confirmedStatusId;
        paymentProof.ReviewedByUserId = userId;
        paymentProof.ReviewedAt = now;
        paymentProof.RejectReason = null;

        reservation.StatusId = confirmedStatusId;
        reservation.HoldExpiresAt = null;
        reservation.RestaurantResponseExpiresAt = null;
        reservation.StatusHistory.Add(new ReservationStatusHistory
        {
            FromStatusId = previousStatusId,
            ToStatusId = confirmedStatusId,
            ChangedByUserId = userId,
            ChangedAt = now,
            Reason = "Ödəniş çeki təsdiqləndi."
        });

        await _reservationRepository.SaveChangesAsync();
        await NotifyReservationCustomerAsync(
            restaurantId,
            reservation,
            NotificationType.ReservationConfirmed,
            "Rezervasiya təsdiqləndi",
            $"Rezervasiya #{reservation.Id} təsdiqləndi.");

        await _auditLogService.RecordRestaurantActionAsync(
            restaurantId,
            AuditActions.ReservationPaymentProofApproved,
            new { reservationId = reservation.Id, paymentProofId = paymentProof.Id },
            AuditEntityTypes.Reservation,
            reservation.Id);

        await transaction.CommitAsync(cancellationToken);

        return BuildActionResponse(
            reservation.Id,
            confirmedStatusId,
            ReservationStatus.Confirmed,
            "Rezervasiya təsdiqləndi.");
    }

    public async Task<ReservationActionResponse> RejectPaymentProofAsync(
        int restaurantId,
        int reservationId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ValidateRestaurantId(restaurantId);
        ValidateReservationId(reservationId);
        var normalizedReason = NormalizeRequiredReason(reason, "Çekin rədd edilmə səbəbini yazın.");
        await EnsureRestaurantReservationAccessAsync(restaurantId);

        var userId = GetCurrentUserId();
        var now = DateTime.UtcNow;
        var paymentSubmittedStatusId = StatusIds.Reservation(ReservationStatus.PaymentSubmitted);
        var pendingPaymentStatusId = StatusIds.Reservation(ReservationStatus.PendingPayment);
        var rejectedStatusId = StatusIds.Reservation(ReservationStatus.Rejected);

        await using var transaction = await _transactionFactory.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var snapshot = await _reservationRepository.GetByIdForRestaurantSnapshotAsync(
            reservationId,
            restaurantId,
            cancellationToken);

        if (snapshot is null)
            throw new NotFoundException(ErrorCode.ReservationNotFound);

        await _tableRepository.AcquireReservationLockAsync(
            restaurantId,
            snapshot.TableId,
            cancellationToken);

        var reservation = await _reservationRepository.GetByIdForRestaurantAsync(
            reservationId,
            restaurantId,
            cancellationToken);

        if (reservation is null)
            throw new NotFoundException(ErrorCode.ReservationNotFound);

        if (reservation.StatusId != paymentSubmittedStatusId)
            throw new BusinessRuleException("Bu rezervasiyanın ödəniş çeki rədd edilə bilməz.");

        await _workflowActionService.EnsureCanExecuteAsync(
            ReservationFlowCode,
            reservation.StatusId,
            "rejectPaymentProof",
            restaurantId,
            reservation.Id);

        var paymentProof = await GetLatestPaymentProofForReviewAsync(
            reservation.Id,
            paymentSubmittedStatusId,
            cancellationToken);

        if (paymentProof is null)
            throw new BusinessRuleException("Rədd ediləcək ödəniş çeki tapılmadı.");

        var previousStatusId = reservation.StatusId;
        paymentProof.StatusId = rejectedStatusId;
        paymentProof.ReviewedByUserId = userId;
        paymentProof.ReviewedAt = now;
        paymentProof.RejectReason = normalizedReason;

        reservation.StatusId = pendingPaymentStatusId;
        reservation.HoldExpiresAt = now.AddMinutes(GetHoldMinutes());
        reservation.RestaurantResponseExpiresAt = null;
        reservation.StatusHistory.Add(new ReservationStatusHistory
        {
            FromStatusId = previousStatusId,
            ToStatusId = pendingPaymentStatusId,
            ChangedByUserId = userId,
            ChangedAt = now,
            Reason = normalizedReason
        });

        await _reservationRepository.SaveChangesAsync();
        await NotifyReservationCustomerAsync(
            restaurantId,
            reservation,
            NotificationType.ReservationPaymentProofRejected,
            "Ödəniş çeki rədd edildi",
            $"Rezervasiya #{reservation.Id} üçün çek rədd edildi: {normalizedReason}");

        await _auditLogService.RecordRestaurantActionAsync(
            restaurantId,
            AuditActions.ReservationPaymentProofRejected,
            new { reservationId = reservation.Id, paymentProofId = paymentProof.Id, reason = normalizedReason },
            AuditEntityTypes.Reservation,
            reservation.Id);

        await transaction.CommitAsync(cancellationToken);

        return BuildActionResponse(
            reservation.Id,
            pendingPaymentStatusId,
            ReservationStatus.PendingPayment,
            "Ödəniş çeki rədd edildi. Müştəri yeni çek göndərə bilər.");
    }

    public async Task<ReservationActionResponse> CancelReservationAsync(
        int reservationId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        ValidateReservationId(reservationId);

        var userId = GetCurrentUserId();
        var snapshot = await _reservationRepository.GetByIdForCustomerSnapshotAsync(
            reservationId,
            userId,
            cancellationToken);

        if (snapshot is null)
            throw new NotFoundException(ErrorCode.ReservationNotFound);

        if (snapshot.CancellationDeadline.HasValue &&
            snapshot.CancellationDeadline.Value <= DateTime.UtcNow)
        {
            throw new BusinessRuleException("Rezervasiyanı ləğv etmək üçün icazə verilən müddət bitib.");
        }

        return await CancelReservationCoreAsync(
            snapshot.RestaurantId,
            reservationId,
            userId,
            reason,
            isCustomer: true,
            cancellationToken);
    }

    public async Task<ReservationActionResponse> CancelRestaurantReservationAsync(
        int restaurantId,
        int reservationId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        ValidateRestaurantId(restaurantId);
        ValidateReservationId(reservationId);
        await EnsureRestaurantReservationAccessAsync(restaurantId);

        return await CancelReservationCoreAsync(
            restaurantId,
            reservationId,
            GetCurrentUserId(),
            reason,
            isCustomer: false,
            cancellationToken);
    }

    private async Task<ReservationActionResponse> CancelReservationCoreAsync(
        int restaurantId,
        int reservationId,
        int userId,
        string? reason,
        bool isCustomer,
        CancellationToken cancellationToken)
    {
        var normalizedReason = NormalizeReason(
            reason,
            isCustomer ? "Müştəri rezervasiyanı ləğv etdi." : "Rezervasiya restoran tərəfindən ləğv edildi.");
        var cancelledStatusId = StatusIds.Reservation(ReservationStatus.Cancelled);
        var paymentSubmittedStatusId = StatusIds.Reservation(ReservationStatus.PaymentSubmitted);

        await using var transaction = await _transactionFactory.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var snapshot = isCustomer
            ? await _reservationRepository.GetByIdForCustomerSnapshotAsync(
                reservationId,
                userId,
                cancellationToken)
            : await _reservationRepository.GetByIdForRestaurantSnapshotAsync(
                reservationId,
                restaurantId,
                cancellationToken);

        if (snapshot is null)
            throw new NotFoundException(ErrorCode.ReservationNotFound);

        await _tableRepository.AcquireReservationLockAsync(
            restaurantId,
            snapshot.TableId,
            cancellationToken);

        var reservation = isCustomer
            ? await _reservationRepository.GetByIdForCustomerForUpdateAsync(
                reservationId,
                restaurantId,
                userId,
                cancellationToken)
            : await _reservationRepository.GetByIdForRestaurantAsync(
                reservationId,
                restaurantId,
                cancellationToken);

        if (reservation is null)
            throw new NotFoundException(ErrorCode.ReservationNotFound);

        await _workflowActionService.EnsureCanExecuteAsync(
            ReservationFlowCode,
            reservation.StatusId,
            "cancel",
            restaurantId,
            reservation.Id);

        if (reservation.StatusId == cancelledStatusId ||
            reservation.StatusId == StatusIds.Reservation(ReservationStatus.Expired))
        {
            throw new BusinessRuleException("Bu rezervasiya artıq aktiv deyil.");
        }

        if (reservation.StatusId == paymentSubmittedStatusId)
        {
            var paymentProof = await GetLatestPaymentProofForReviewAsync(
                reservation.Id,
                paymentSubmittedStatusId,
                cancellationToken);

            if (paymentProof is not null)
            {
                paymentProof.StatusId = StatusIds.Reservation(ReservationStatus.Rejected);
                paymentProof.ReviewedByUserId = userId;
                paymentProof.ReviewedAt = DateTime.UtcNow;
                paymentProof.RejectReason = normalizedReason;
            }
        }

        var previousStatusId = reservation.StatusId;
        reservation.StatusId = cancelledStatusId;
        reservation.HoldExpiresAt = null;
        reservation.RestaurantResponseExpiresAt = null;
        reservation.StatusHistory.Add(new ReservationStatusHistory
        {
            FromStatusId = previousStatusId,
            ToStatusId = cancelledStatusId,
            ChangedByUserId = userId,
            ChangedAt = DateTime.UtcNow,
            Reason = normalizedReason
        });

        await _reservationRepository.SaveChangesAsync();

        if (isCustomer)
        {
            await NotifyReservationResponsibleUsersAsync(
                restaurantId,
                reservation,
                "Rezervasiya ləğv edildi",
                $"Müştəri rezervasiya #{reservation.Id} üçün ləğv sorğusu göndərdi.");
        }
        else
        {
            await NotifyReservationCustomerAsync(
                restaurantId,
                reservation,
                NotificationType.ReservationCancelled,
                "Rezervasiya ləğv edildi",
                $"Rezervasiya #{reservation.Id} restoran tərəfindən ləğv edildi.");
        }

        await _auditLogService.RecordRestaurantActionAsync(
            restaurantId,
            AuditActions.ReservationCancelled,
            new { reservationId = reservation.Id, reason = normalizedReason, initiatedByCustomer = isCustomer },
            AuditEntityTypes.Reservation,
            reservation.Id);

        await transaction.CommitAsync(cancellationToken);

        return BuildActionResponse(
            reservation.Id,
            cancelledStatusId,
            ReservationStatus.Cancelled,
            "Rezervasiya ləğv edildi.");
    }

    private PaginatedList<ReservationResponse> MapPage(
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
