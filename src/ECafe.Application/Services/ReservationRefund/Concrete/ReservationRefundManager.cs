using System.Data;
using System.Text.RegularExpressions;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Repositories.Reservation;
using ECafe.Application.Repositories.ReservationPaymentProof;
using ECafe.Application.Repositories.ReservationRefund;
using ECafe.Application.Repositories.File;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.ReservationRefund.Abstract;
using ECafe.Application.Services.RefundPayoutDetails.Abstract;
using ECafe.Application.Services.Workflow.Abstract;
using ECafe.Application.Services.FileAccess.Abstract;
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
    private static readonly Regex ProhibitedPayoutDetailsPattern = new(
        @"\b(cvv|cvc|pin)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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
    private readonly IFileRepository _fileRepository;
    private readonly IUserRestaurantRepository _userRestaurantRepository;
    private readonly IApplicationDbTransactionFactory _transactionFactory;
    private readonly IWorkflowActionService _workflowActionService;
    private readonly IRefundPayoutDetailsProtector _payoutDetailsProtector;
    private readonly IFileAccessUrlService _fileAccessUrlService;
    private readonly IReservationRefundNotifier _refundNotifier;
    private readonly IAuditLogService _auditLogService;

    public ReservationRefundManager(
        IHttpContextAccessor httpContextAccessor,
        AutoMapper.IMapper mapper,
        IConfiguration configuration,
        IReservationRepository reservationRepository,
        IReservationPaymentProofRepository paymentProofRepository,
        IReservationRefundRepository refundRepository,
        IFileRepository fileRepository,
        IUserRestaurantRepository userRestaurantRepository,
        IApplicationDbTransactionFactory transactionFactory,
        IWorkflowActionService workflowActionService,
        IRefundPayoutDetailsProtector payoutDetailsProtector,
        IFileAccessUrlService fileAccessUrlService,
        IReservationRefundNotifier refundNotifier,
        IAuditLogService auditLogService)
        : base(httpContextAccessor, mapper, configuration)
    {
        _reservationRepository = reservationRepository;
        _paymentProofRepository = paymentProofRepository;
        _refundRepository = refundRepository;
        _fileRepository = fileRepository;
        _userRestaurantRepository = userRestaurantRepository;
        _transactionFactory = transactionFactory;
        _workflowActionService = workflowActionService;
        _payoutDetailsProtector = payoutDetailsProtector;
        _fileAccessUrlService = fileAccessUrlService;
        _refundNotifier = refundNotifier;
        _auditLogService = auditLogService;
    }

    public async Task<ReservationRefundResponse?> GetMyByReservationAsync(
        int reservationId,
        CancellationToken cancellationToken = default)
    {
        ValidateReservationId(reservationId);
        var userId = GetCurrentUserId();

        var refund = await _refundRepository.GetByReservationForCustomerAsync(
            reservationId,
            userId,
            cancellationToken);

        return refund is null ? null : ReservationRefundResponseMapper.Map(refund, _fileAccessUrlService);
    }

    public async Task<ReservationRefundResponse?> GetRestaurantByReservationAsync(
        int restaurantId,
        int reservationId,
        CancellationToken cancellationToken = default)
    {
        ValidateRestaurantId(restaurantId);
        ValidateReservationId(reservationId);
        await EnsureRestaurantAccessAsync(restaurantId);

        var refund = await _refundRepository.GetByReservationForRestaurantAsync(
            restaurantId,
            reservationId,
            cancellationToken);

        return refund is null ? null : ReservationRefundResponseMapper.Map(refund, _fileAccessUrlService);
    }

    public async Task<RestaurantReservationRefundPayoutDetailsResponse> GetPayoutDetailsForRestaurantAsync(
        int restaurantId,
        int refundId,
        CancellationToken cancellationToken = default)
    {
        ValidateRestaurantId(restaurantId);
        ValidateRefundId(refundId);
        if (IsCurrentUserSuperAdmin())
            throw new ForbiddenException(ErrorCode.OnlyRestaurantManagersCanManageRefund);

        await EnsureRestaurantAccessAsync(restaurantId);

        var refund = await _refundRepository.GetByIdForRestaurantSnapshotAsync(
            restaurantId,
            refundId,
            cancellationToken);

        if (refund is null)
            throw new NotFoundException(ErrorCode.ReservationRefundNotFound);

        if (refund.PayoutDetails is null)
            throw new BusinessRuleException(ErrorCode.RefundPayoutDetailsNotSubmitted);

        var details = _payoutDetailsProtector.Unprotect(refund.PayoutDetails.EncryptedDetails);

        await _auditLogService.RecordRestaurantActionAsync(
            restaurantId,
            AuditActions.ReservationRefundPayoutDetailsViewed,
            new
            {
                reservationId = refund.ReservationId,
                refundId = refund.Id
            },
            AuditEntityTypes.ReservationRefund,
            refund.Id);

        return new RestaurantReservationRefundPayoutDetailsResponse
        {
            RefundId = refund.Id,
            Details = details,
            SubmittedAt = ReservationRefundResponseMapper.ToUtcOffset(refund.PayoutDetails.SubmittedAt)
        };
    }

    public async Task<ReservationRefundResponse> RequestAsync(
        int reservationId,
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
            throw new BusinessRuleException(ErrorCode.ReservationRefundNotEligible);

        if (await _refundRepository.HasForReservationAsync(reservation.Id, cancellationToken))
            throw new BusinessRuleException(ErrorCode.ReservationRefundAlreadyRequested);

        var sourcePaymentProof = await _paymentProofRepository.GetLatestConfirmedByReservationAsync(
            reservation.Id,
            cancellationToken);

        if (sourcePaymentProof is null)
            throw new BusinessRuleException(ErrorCode.ConfirmedReservationDepositNotFound);

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

        await _refundNotifier.NotifyRestaurantAsync(
            reservation,
            refund,
            NotificationType.ReservationRefundRequested,
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

        return ReservationRefundResponseMapper.Map(refund, _fileAccessUrlService);
    }

    public async Task<ReservationRefundResponse> SubmitPayoutDetailsAsync(
        int refundId,
        string details,
        CancellationToken cancellationToken = default)
    {
        ValidateRefundId(refundId);
        var normalizedDetails = NormalizePayoutDetails(details);
        var userId = GetCurrentUserId();

        var snapshot = await _refundRepository.GetByIdForCustomerSnapshotAsync(
            refundId,
            userId,
            cancellationToken);

        if (snapshot is null)
            throw new NotFoundException(ErrorCode.ReservationRefundNotFound);

        await using var transaction = await _transactionFactory.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        await _reservationRepository.AcquireCustomerReservationLockAsync(
            snapshot.Reservation.RestaurantId,
            userId,
            cancellationToken);

        var refund = await _refundRepository.GetByIdForCustomerForUpdateAsync(
            refundId,
            userId,
            cancellationToken);

        if (refund is null)
            throw new NotFoundException(ErrorCode.ReservationRefundNotFound);

        await _workflowActionService.EnsureCanExecuteAsync(
            RefundFlowCode,
            refund.StatusId,
            WorkflowActionCode.Refund.SubmitPayoutDetails,
            refund.Reservation.RestaurantId,
            refund.Id);

        var now = DateTime.UtcNow;
        var readyForPayoutStatusId = StatusIds.Refund(RefundStatus.ReadyForPayout);
        refund.PayoutDetails = new ReservationRefundPayoutDetail
        {
            EncryptedDetails = _payoutDetailsProtector.Protect(normalizedDetails),
            MaskedDetails = _payoutDetailsProtector.CreateMaskedDetails(normalizedDetails),
            SubmittedByUserId = userId,
            SubmittedAt = now
        };
        ReservationRefundStatusTransition.Apply(
            refund,
            readyForPayoutStatusId,
            userId,
            now,
            "Müştəri geri ödəniş məlumatlarını göndərdi. Restoran ödənişi həyata keçirə bilər.");

        await _refundRepository.SaveChangesAsync();

        await _refundNotifier.NotifyRestaurantAsync(
            refund.Reservation,
            refund,
            NotificationType.ReservationRefundPayoutDetailsSubmitted,
            "Geri ödəniş məlumatları göndərildi",
            $"Rezervasiya #{refund.ReservationId} üçün geri ödəniş məlumatları göndərildi. {refund.Amount:0.00} {refund.CurrencyCode} ödənişini həyata keçirə bilərsiniz.");

        await _auditLogService.RecordRestaurantActionAsync(
            refund.Reservation.RestaurantId,
            AuditActions.ReservationRefundPayoutDetailsSubmitted,
            new
            {
                reservationId = refund.ReservationId,
                refundId = refund.Id,
                amount = refund.Amount
            },
            AuditEntityTypes.ReservationRefund,
            refund.Id);

        await transaction.CommitAsync(cancellationToken);

        return ReservationRefundResponseMapper.Map(refund, _fileAccessUrlService);
    }

    public async Task EnsureTransferCanBeSubmittedAsync(
        int restaurantId,
        int refundId,
        CancellationToken cancellationToken = default)
    {
        ValidateRestaurantId(restaurantId);
        ValidateRefundId(refundId);
        await EnsureRestaurantAccessAsync(restaurantId);

        var refund = await _refundRepository.GetByIdForRestaurantSnapshotAsync(
            restaurantId,
            refundId,
            cancellationToken);

        if (refund is null)
            throw new NotFoundException(ErrorCode.ReservationRefundNotFound);

        await _workflowActionService.EnsureCanExecuteAsync(
            RefundFlowCode,
            refund.StatusId,
            WorkflowActionCode.Refund.SubmitTransfer,
            restaurantId,
            refund.Id);
    }

    public async Task<ReservationRefundTransferResponse> SubmitTransferAsync(
        int restaurantId,
        int refundId,
        string? transferReference,
        int proofFileId,
        CancellationToken cancellationToken = default)
    {
        ValidateRestaurantId(restaurantId);
        ValidateRefundId(refundId);
        if (proofFileId <= 0)
            throw new BadRequestException(ErrorCode.RefundTransferProofRequired);

        await EnsureRestaurantAccessAsync(restaurantId);
        var normalizedTransferReference = NormalizeTransferReference(transferReference);
        var userId = GetCurrentUserId();

        await using var transaction = await _transactionFactory.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        await _refundRepository.AcquireRefundLockAsync(refundId, cancellationToken);

        var refund = await _refundRepository.GetByIdForRestaurantForUpdateAsync(
            restaurantId,
            refundId,
            cancellationToken);

        if (refund is null)
            throw new NotFoundException(ErrorCode.ReservationRefundNotFound);

        await _workflowActionService.EnsureCanExecuteAsync(
            RefundFlowCode,
            refund.StatusId,
            WorkflowActionCode.Refund.SubmitTransfer,
            restaurantId,
            refund.Id);

        var proofFile = await _fileRepository.GetAttachableByIdAsync(proofFileId);
        if (proofFile is null ||
            proofFile.FileTypeId != (int)FileTypeCode.PaymentReceipt ||
            !string.Equals(proofFile.CreatedBy, userId.ToString(), StringComparison.Ordinal))
        {
            throw new BusinessRuleException(ErrorCode.FileNotFoundOrAlreadyAttached);
        }

        var now = DateTime.UtcNow;
        var processingStatusId = StatusIds.Refund(RefundStatus.Processing);
        var transfer = new ReservationRefundTransfer
        {
            Amount = refund.Amount,
            TransferReference = normalizedTransferReference,
            ProofFileId = proofFile.Id,
            SubmittedByUserId = userId,
            SubmittedAt = now
        };
        refund.TransferAttempts.Add(transfer);
        ReservationRefundStatusTransition.Apply(
            refund,
            processingStatusId,
            userId,
            now,
            "Restoran geri ödəniş çekini göndərdi. Müştərinin təsdiqi gözlənilir.");

        await _refundRepository.SaveChangesAsync();

        await _refundNotifier.NotifyCustomerTransferSubmittedAsync(refund, transfer);

        await _auditLogService.RecordRestaurantActionAsync(
            restaurantId,
            AuditActions.ReservationRefundTransferSubmitted,
            new
            {
                reservationId = refund.ReservationId,
                refundId = refund.Id,
                refundTransferId = transfer.Id,
                proofFileId = proofFile.Id,
                amount = transfer.Amount
            },
            AuditEntityTypes.ReservationRefund,
            refund.Id);

        await transaction.CommitAsync(cancellationToken);

        return ReservationRefundResponseMapper.MapTransfer(refund, transfer, _fileAccessUrlService);
    }

    public Task<ReservationRefundResponse> ConfirmTransferAsync(
        int refundId,
        int transferId,
        CancellationToken cancellationToken = default)
        => ReviewTransferAsync(refundId, transferId, null, cancellationToken);

    public Task<ReservationRefundResponse> DisputeTransferAsync(
        int refundId,
        int transferId,
        string reason,
        CancellationToken cancellationToken = default)
        => ReviewTransferAsync(refundId, transferId, NormalizeDisputeReason(reason), cancellationToken);

    private async Task<ReservationRefundResponse> ReviewTransferAsync(
        int refundId,
        int transferId,
        string? disputeReason,
        CancellationToken cancellationToken)
    {
        ValidateRefundId(refundId);
        if (transferId <= 0)
            throw new BadRequestException(ErrorCode.InvalidRefundTransferId);

        var userId = GetCurrentUserId();
        var isDispute = disputeReason is not null;

        await using var transaction = await _transactionFactory.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        await _refundRepository.AcquireRefundLockAsync(refundId, cancellationToken);
        var refund = await _refundRepository.GetByIdForCustomerForUpdateAsync(
            refundId,
            userId,
            cancellationToken);

        if (refund is null)
            throw new NotFoundException(ErrorCode.ReservationRefundNotFound);

        var latestTransfer = refund.TransferAttempts
            .OrderByDescending(transfer => transfer.SubmittedAt)
            .ThenByDescending(transfer => transfer.Id)
            .FirstOrDefault();

        if (latestTransfer?.Id != transferId)
            throw new BusinessRuleException(ErrorCode.RefundTransferNotCurrent);

        if (!isDispute &&
            refund.StatusId == StatusIds.Refund(RefundStatus.Refunded) &&
            latestTransfer.CustomerConfirmedAt.HasValue)
        {
            return ReservationRefundResponseMapper.Map(refund, _fileAccessUrlService);
        }

        if (isDispute &&
            refund.StatusId == StatusIds.Refund(RefundStatus.Disputed) &&
            latestTransfer.DisputedAt.HasValue &&
            string.Equals(latestTransfer.DisputeReason, disputeReason, StringComparison.Ordinal))
        {
            return ReservationRefundResponseMapper.Map(refund, _fileAccessUrlService);
        }

        await _workflowActionService.EnsureCanExecuteAsync(
            RefundFlowCode,
            refund.StatusId,
            isDispute ? WorkflowActionCode.Refund.DisputeTransfer : WorkflowActionCode.Refund.ConfirmTransfer,
            refund.Reservation.RestaurantId,
            refund.Id);

        var now = DateTime.UtcNow;
        if (isDispute)
        {
            latestTransfer.DisputedAt = now;
            latestTransfer.DisputeReason = disputeReason;
        }
        else
        {
            latestTransfer.CustomerConfirmedAt = now;
            refund.RefundedAt = now;
        }

        ReservationRefundStatusTransition.Apply(
            refund,
            StatusIds.Refund(isDispute ? RefundStatus.Disputed : RefundStatus.Refunded),
            userId,
            now,
            isDispute ? disputeReason! : "Müştəri geri ödənişin hesabına çatdığını təsdiqlədi.");

        await _refundRepository.SaveChangesAsync();

        await _refundNotifier.NotifyRestaurantAsync(
            refund.Reservation,
            refund,
            isDispute
                ? NotificationType.ReservationRefundTransferDisputed
                : NotificationType.ReservationRefundTransferConfirmed,
            isDispute ? "Geri ödənişə etiraz edildi" : "Geri ödəniş təsdiqləndi",
            isDispute
                ? $"Rezervasiya #{refund.ReservationId} üçün geri ödənişə etiraz edildi: {disputeReason}"
                : $"Rezervasiya #{refund.ReservationId} üçün geri ödəniş müştəri tərəfindən təsdiqləndi.");

        await _auditLogService.RecordRestaurantActionAsync(
            refund.Reservation.RestaurantId,
            isDispute
                ? AuditActions.ReservationRefundTransferDisputed
                : AuditActions.ReservationRefundTransferConfirmed,
            new
            {
                reservationId = refund.ReservationId,
                refundId = refund.Id,
                refundTransferId = latestTransfer.Id,
                reason = disputeReason
            },
            AuditEntityTypes.ReservationRefund,
            refund.Id);

        await transaction.CommitAsync(cancellationToken);
        return ReservationRefundResponseMapper.Map(refund, _fileAccessUrlService);
    }

    private async Task EnsureRestaurantAccessAsync(int restaurantId)
    {
        if (IsCurrentUserSuperAdmin())
            return;

        var userId = GetCurrentUserId();
        if (!await _userRestaurantRepository.UserBelogsToRestaurantAsync(userId, restaurantId))
            throw new BusinessRuleException(ErrorCode.UserNotBelongsToRestaurant);

        if (!RestaurantManagerRoleIds.Contains(GetCurrentRoleId(restaurantId)))
            throw new ForbiddenException(ErrorCode.OnlyRestaurantManagersCanManageRefund);
    }

    private static string BuildEligibilityReason(ReservationEntity reservation)
    {
        return reservation.CancelledByUserId == reservation.CustomerUserId
            ? "Müştəri rezervasiyanı icazə verilən ləğv müddəti daxilində ləğv edib."
            : "Restoran təsdiqlənmiş rezervasiyanı ləğv edib.";
    }

    private static void ValidateReservationId(int reservationId)
    {
        if (reservationId <= 0)
            throw new BadRequestException(ErrorCode.InvalidReservationId);
    }

    private static void ValidateRefundId(int refundId)
    {
        if (refundId <= 0)
            throw new BadRequestException(ErrorCode.InvalidReservationRefundId);
    }

    private static string NormalizePayoutDetails(string? details)
    {
        var normalizedDetails = details?.Trim() ?? string.Empty;

        if (normalizedDetails.Length is < 4 or > 1000)
            throw new BadRequestException(ErrorCode.InvalidRefundPayoutDetails);

        if (ProhibitedPayoutDetailsPattern.IsMatch(normalizedDetails))
        {
            throw new BadRequestException(ErrorCode.RefundPayoutSensitiveDataProhibited);
        }

        return normalizedDetails;
    }

    private static string? NormalizeTransferReference(string? transferReference)
    {
        var normalizedReference = transferReference?.Trim();
        if (normalizedReference?.Length > 100)
            throw new BadRequestException(ErrorCode.InvalidRefundTransferReference);

        return string.IsNullOrWhiteSpace(normalizedReference) ? null : normalizedReference;
    }

    private static string NormalizeDisputeReason(string? reason)
    {
        var normalizedReason = reason?.Trim() ?? string.Empty;
        if (normalizedReason.Length is < 5 or > 500)
            throw new BadRequestException(ErrorCode.InvalidRefundTransferDisputeReason);

        return normalizedReason;
    }

    private static void ValidateRestaurantId(int restaurantId)
    {
        if (restaurantId <= 0)
            throw new BadRequestException(ErrorCode.InvalidRestaurantId);
    }

}
