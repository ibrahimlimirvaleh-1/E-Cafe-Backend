using System.Data;
using System.Text.Json;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Repositories.Reservation;
using ECafe.Application.Repositories.ReservationPaymentProof;
using ECafe.Application.Repositories.ReservationRefund;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.ReservationRefund.Abstract;
using ECafe.Application.Services.Workflow.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Workflow;
using ECafe.Shared.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using ReservationEntity = ECafe.Domain.Entities.Reservation;
using ReservationRefundEntity = ECafe.Domain.Entities.ReservationRefund;
using StatusTypeEnum = ECafe.Domain.Enums.StatusType;

namespace ECafe.Application.Services.ReservationRefund.Concrete;

public sealed class ReservationRefundManager : BaseManager, IReservationRefundService
{
    private static readonly int[] RestaurantManagerRoleIds =
    [
        (int)RoleCode.Manager,
        (int)RoleCode.Owner
    ];

    private static string ReservationFlowCode
        => WorkflowFlowCode.FromStatusType(StatusTypeEnum.Reservation);

    private static string RefundFlowCode
        => WorkflowFlowCode.FromStatusType(StatusTypeEnum.Refund);

    private readonly IReservationRepository _reservationRepository;
    private readonly IReservationPaymentProofRepository _paymentProofRepository;
    private readonly IReservationRefundRepository _refundRepository;
    private readonly IUserRestaurantRepository _userRestaurantRepository;
    private readonly IApplicationDbTransactionFactory _transactionFactory;
    private readonly IWorkflowActionService _workflowActionService;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLogService;

    public ReservationRefundManager(
        IHttpContextAccessor httpContextAccessor,
        AutoMapper.IMapper mapper,
        IConfiguration configuration,
        IReservationRepository reservationRepository,
        IReservationPaymentProofRepository paymentProofRepository,
        IReservationRefundRepository refundRepository,
        IUserRestaurantRepository userRestaurantRepository,
        IApplicationDbTransactionFactory transactionFactory,
        IWorkflowActionService workflowActionService,
        INotificationService notificationService,
        IAuditLogService auditLogService)
        : base(httpContextAccessor, mapper, configuration)
    {
        _reservationRepository = reservationRepository;
        _paymentProofRepository = paymentProofRepository;
        _refundRepository = refundRepository;
        _userRestaurantRepository = userRestaurantRepository;
        _transactionFactory = transactionFactory;
        _workflowActionService = workflowActionService;
        _notificationService = notificationService;
        _auditLogService = auditLogService;
    }

    public async Task<ReservationRefundResponse?> GetMyByReservationAsync(
        int reservationId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(reservationId, "Rezervasiya seçimi düzgün deyil.");
        var userId = GetCurrentUserId();

        var refund = await _refundRepository.GetByReservationForCustomerAsync(
            reservationId,
            userId,
            cancellationToken);

        return refund is null ? null : MapResponse(refund);
    }

    public async Task<ReservationRefundResponse?> GetRestaurantByReservationAsync(
        int restaurantId,
        int reservationId,
        CancellationToken cancellationToken = default)
    {
        ValidateRestaurantId(restaurantId);
        ValidateId(reservationId, "Rezervasiya seçimi düzgün deyil.");
        await EnsureRestaurantAccessAsync(restaurantId);

        var refund = await _refundRepository.GetByReservationForRestaurantAsync(
            restaurantId,
            reservationId,
            cancellationToken);

        return refund is null ? null : MapResponse(refund);
    }

    public async Task<ReservationRefundResponse> RequestAsync(
        int reservationId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(reservationId, "Rezervasiya seçimi düzgün deyil.");
        var userId = GetCurrentUserId();

        var snapshot = await _reservationRepository.GetByIdForCustomerSnapshotAsync(
            reservationId,
            userId,
            cancellationToken);

        if (snapshot is null)
            throw new NotFoundException(ErrorCode.ReservationNotFound);

        await using var transaction = await _transactionFactory.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        await _reservationRepository.AcquireCustomerReservationLockAsync(
            snapshot.RestaurantId,
            userId,
            cancellationToken);

        var reservation = await _reservationRepository.GetByIdForCustomerForUpdateAsync(
            reservationId,
            snapshot.RestaurantId,
            userId,
            cancellationToken);

        if (reservation is null)
            throw new NotFoundException(ErrorCode.ReservationNotFound);

        await _workflowActionService.EnsureCanExecuteAsync(
            ReservationFlowCode,
            reservation.StatusId,
            WorkflowActionCode.Reservation.RequestRefund,
            reservation.RestaurantId,
            reservation.Id);

        if (reservation.RefundEligible != true)
            throw new BusinessRuleException("Bu rezervasiya üçün geri ödəniş əlçatan deyil.");

        if (await _refundRepository.HasForReservationAsync(reservation.Id, cancellationToken))
            throw new BusinessRuleException("Bu rezervasiya üçün artıq geri ödəniş sorğusu mövcuddur.");

        var sourcePaymentProof = await _paymentProofRepository.GetLatestConfirmedByReservationAsync(
            reservation.Id,
            cancellationToken);

        if (sourcePaymentProof is null)
            throw new BusinessRuleException("Təsdiqlənmiş depozit ödənişi tapılmadı.");

        var now = DateTime.UtcNow;
        var awaitingPayoutDetailsStatusId = StatusIds.Refund(RefundStatus.AwaitingPayoutDetails);
        var refund = new ReservationRefundEntity
        {
            ReservationId = reservation.Id,
            SourcePaymentProofId = sourcePaymentProof.Id,
            StatusId = awaitingPayoutDetailsStatusId,
            Amount = sourcePaymentProof.Amount,
            CurrencyCode = "AZN",
            InitiatedByUserId = userId,
            RequestedAt = now,
            EligibilityReason = BuildEligibilityReason(reservation),
            CancellationReasonSnapshot = reservation.CancelReason,
            StatusHistory =
            [
                new ReservationRefundStatusHistory
                {
                    ToStatusId = awaitingPayoutDetailsStatusId,
                    ChangedByUserId = userId,
                    ChangedAt = now,
                    Reason = "Geri ödəniş hüququ avtomatik təsdiqləndi. Ödəniş məlumatları gözlənilir."
                }
            ]
        };

        await _refundRepository.Add(refund);
        await _refundRepository.SaveChangesAsync();

        await NotifyRestaurantAsync(
            reservation,
            refund,
            "Geri ödəniş prosesi başladı",
            $"Rezervasiya #{reservation.Id} üçün {refund.Amount:0.00} {refund.CurrencyCode} geri ödənişi avtomatik təsdiqləndi. Müştərinin ödəniş məlumatları gözlənilir.");

        await _auditLogService.RecordRestaurantActionAsync(
            reservation.RestaurantId,
            AuditActions.ReservationRefundRequested,
            new
            {
                reservationId = reservation.Id,
                refundId = refund.Id,
                sourcePaymentProofId = sourcePaymentProof.Id,
                amount = refund.Amount
            },
            AuditEntityTypes.ReservationRefund,
            refund.Id);

        await transaction.CommitAsync(cancellationToken);

        return MapResponse(refund);
    }

    private async Task EnsureRestaurantAccessAsync(int restaurantId)
    {
        if (IsCurrentUserSuperAdmin())
            return;

        var userId = GetCurrentUserId();
        if (!await _userRestaurantRepository.UserBelogsToRestaurantAsync(userId, restaurantId))
            throw new BusinessRuleException(ErrorCode.UserNotBelongsToRestaurant);

        if (!RestaurantManagerRoleIds.Contains(GetCurrentRoleId(restaurantId)))
            throw new ForbiddenException("Yalnız restoran meneceri geri ödəniş sorğularını idarə edə bilər.");
    }

    private async Task NotifyRestaurantAsync(
        ReservationEntity reservation,
        ReservationRefundEntity refund,
        string title,
        string message)
    {
        var assignments = await _userRestaurantRepository.GetActiveByRestaurantAndRolesAsync(
            reservation.RestaurantId,
            RestaurantManagerRoleIds);

        if (assignments.Count == 0)
        {
            var ownerAssignment = await _userRestaurantRepository
                .GetActiveOwnerByRestaurantAsync(reservation.RestaurantId);

            if (ownerAssignment is not null)
                assignments.Add(ownerAssignment);
        }

        foreach (var assignment in assignments)
        {
            await _notificationService.CreateAsync(new CreateNotificationRequest
            {
                UserId = assignment.UserId,
                RestaurantId = reservation.RestaurantId,
                Title = title,
                Message = message,
                TypeId = (int)NotificationType.ReservationRefundRequested,
                ChannelId = (int)NotificationChannel.InApp,
                PayloadJson = SerializeNotificationPayload(reservation.RestaurantId, reservation.Id, refund),
                RelatedEntityType = AuditEntityTypes.ReservationRefund,
                RelatedEntityId = refund.Id
            });
        }
    }

    private static string SerializeNotificationPayload(
        int restaurantId,
        int reservationId,
        ReservationRefundEntity refund)
    {
        return JsonSerializer.Serialize(new
        {
            restaurantId,
            reservationId,
            refundId = refund.Id,
            refundStatusId = refund.StatusId
        });
    }

    private static ReservationRefundResponse MapResponse(ReservationRefundEntity refund)
    {
        return new ReservationRefundResponse
        {
            Id = refund.Id,
            ReservationId = refund.ReservationId,
            SourcePaymentProofId = refund.SourcePaymentProofId,
            StatusId = refund.StatusId,
            Status = refund.Status?.Name ?? RefundStatus.AwaitingPayoutDetails.GetName(),
            WorkflowFlowCode = RefundFlowCode,
            Amount = refund.Amount,
            CurrencyCode = refund.CurrencyCode,
            RequestedAt = ToUtcOffset(refund.RequestedAt),
            ApprovedAt = ToNullableUtcOffset(refund.ApprovedAt),
            RefundedAt = ToNullableUtcOffset(refund.RefundedAt),
            EligibilityReason = refund.EligibilityReason,
            CancellationReason = refund.CancellationReasonSnapshot,
            History = refund.StatusHistory
                .OrderBy(history => history.ChangedAt)
                .ThenBy(history => history.Id)
                .Select(history => new ReservationRefundStatusHistoryResponse
                {
                    Id = history.Id,
                    FromStatus = history.FromStatus?.Name,
                    ToStatus = history.ToStatus?.Name ?? RefundStatus.AwaitingPayoutDetails.GetName(),
                    ChangedAt = ToUtcOffset(history.ChangedAt),
                    ActorType = history.ChangedByUserId is null
                        ? "System"
                        : history.ChangedByUserId == refund.Reservation.CustomerUserId
                            ? "Customer"
                            : "Restaurant",
                    Reason = history.Reason
                })
                .ToList()
        };
    }

    private static string BuildEligibilityReason(ReservationEntity reservation)
    {
        return reservation.CancelledByUserId == reservation.CustomerUserId
            ? "Müştəri rezervasiyanı icazə verilən ləğv müddəti daxilində ləğv edib."
            : "Restoran təsdiqlənmiş rezervasiyanı ləğv edib.";
    }

    private static void ValidateId(int value, string message)
    {
        if (value <= 0)
            throw new BadRequestException(message);
    }

    private static void ValidateRestaurantId(int restaurantId)
    {
        if (restaurantId <= 0)
            throw new BadRequestException("Restoran seçimi düzgün deyil.");
    }

    private static DateTimeOffset ToUtcOffset(DateTime value)
        => new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);

    private static DateTimeOffset? ToNullableUtcOffset(DateTime? value)
        => value.HasValue ? ToUtcOffset(value.Value) : null;

}
